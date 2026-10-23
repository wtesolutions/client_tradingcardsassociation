using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Data;
using SocketLabs.InjectionApi;
using SocketLabs.InjectionApi.Message;

namespace Ntca.Web.Services;

public sealed class CompositeEmailSender : IAppEmailSender
{
    private readonly SocketLabsOptions _socket;
    private readonly EmailOptions _email;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CompositeEmailSender> _log;

    public CompositeEmailSender(
        IOptions<SocketLabsOptions> socket,
        IOptions<EmailOptions> email,
        IWebHostEnvironment env,
        ILogger<CompositeEmailSender> log)
    {
        _socket = socket.Value;
        _email = email.Value;
        _env = env;
        _log = log;
    }

    public bool IsConfigured =>
        _socket.ServerId > 0 && !string.IsNullOrWhiteSpace(_socket.ApiKey);

    public async Task SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null, CancellationToken ct = default)
    {
        if (IsConfigured)
        {
            try
            {
                var client = new SocketLabsClient(_socket.ServerId, _socket.ApiKey);
                var message = new BasicMessage
                {
                    Subject = subject,
                    PlainTextBody = textBody,
                    HtmlBody = htmlBody ?? $"<pre>{System.Net.WebUtility.HtmlEncode(textBody)}</pre>"
                };
                message.From.Email = _email.FromAddress;
                message.From.FriendlyName = _email.FromName;
                message.To.Add(toEmail);
                var response = await Task.Run(() => client.Send(message), ct);
                _log.LogInformation(
                    "SocketLabs send {Result} to {To} subject {Subject} (from {From}, serverId set={HasServer})",
                    response.Result, toEmail, subject, _email.FromAddress, _socket.ServerId > 0);
                if (response.Result != SendResult.Success)
                {
                    _log.LogWarning("SocketLabs send did not succeed: {Result} {Error}", response.Result, response.ResponseMessage);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SocketLabs send failed to {To} subject {Subject}", toEmail, subject);
                throw;
            }
            return;
        }

        var pickup = Path.Combine(_env.ContentRootPath, "App_Data", "mail");
        Directory.CreateDirectory(pickup);
        var file = Path.Combine(pickup, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.txt");
        var payload = $"From: {_email.FromName} <{_email.FromAddress}>\nTo: {toEmail}\nSubject: {subject}\n\n{textBody}\n";
        await File.WriteAllTextAsync(file, payload, ct);
        _log.LogInformation(
            "Email fallback (pickup-directory) wrote {File} to {To} subject {Subject}. SocketLabs ServerId/ApiKey are empty.",
            Path.GetFileName(file), toEmail, subject);
        if (_env.IsDevelopment())
        {
            _log.LogInformation("DEV email body to {To}:\n{Body}", toEmail, textBody);
        }
    }
}

public sealed class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _opt;
    private readonly IWebHostEnvironment _env;

    public LocalFileStorage(IOptions<StorageOptions> opt, IWebHostEnvironment env)
    {
        _opt = opt.Value;
        _env = env;
    }

    public bool UsesObjectStorage => !string.IsNullOrWhiteSpace(_opt.S3Bucket);

    public async Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken ct = default)
    {
        var root = Path.IsPathRooted(_opt.LocalRoot)
            ? _opt.LocalRoot
            : Path.Combine(_env.ContentRootPath, _opt.LocalRoot);
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', '/')));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return "/uploads/" + relativePath.Replace('\\', '/').TrimStart('/');
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Resolve(relativePath)));

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full))
            File.Delete(full);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Resolve(relativePath);
        if (!File.Exists(full))
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(full));
    }

    private string Resolve(string relativePath)
    {
        var root = Path.IsPathRooted(_opt.LocalRoot)
            ? _opt.LocalRoot
            : Path.Combine(_env.ContentRootPath, _opt.LocalRoot);
        return Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', '/')));
    }
}

public sealed class LoginCodeService
{
    private readonly NtcaDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LoginCodeService> _log;

    public LoginCodeService(NtcaDbContext db, IAppEmailSender email, IWebHostEnvironment env, ILogger<LoginCodeService> log)
    {
        _db = db;
        _email = email;
        _env = env;
        _log = log;
    }

    public static byte[] HashCode(string email, string code)
    {
        var material = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant() + ":" + code.Trim());
        return SHA256.HashData(material);
    }

    public async Task<string> IssueAsync(string email, string purpose, long? userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.VerificationCodes
            .Where(c => c.Email == email && c.Purpose == purpose && c.UsedAt == null && c.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var row in existing)
            row.RevokedAt = now;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _db.VerificationCodes.Add(new VerificationCode
        {
            UserId = userId,
            Email = email,
            Purpose = purpose,
            CodeHash = HashCode(email, code),
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(ct);

        if (_env.IsDevelopment())
            _log.LogWarning("DEV login code for {Email} ({Purpose}): {Code}", email, purpose, code);

        var subject = purpose == "SADMIN_LOGIN"
            ? "Your NTCA staff sign-in code"
            : "Your NTCA shop-owner sign-in code";
        var body = $"Your National Trading Card Association sign-in code is {code}. It expires in 15 minutes.\n\nIf you did not request this, ignore this email.";
        await _email.SendAsync(email, subject, body, ct: ct);
        return code;
    }

    public async Task<bool> VerifyAsync(string email, string purpose, string code, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var hash = HashCode(email, code);
        var row = await _db.VerificationCodes
            .Where(c => c.Email == email && c.Purpose == purpose && c.UsedAt == null && c.RevokedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return false;
        row.AttemptCount = (byte)Math.Min(255, row.AttemptCount + 1);
        if (!row.CodeHash.SequenceEqual(hash) || row.AttemptCount > 5)
        {
            if (row.AttemptCount > 5)
                row.RevokedAt = now;
            await _db.SaveChangesAsync(ct);
            return false;
        }
        row.UsedAt = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class SiteModeService
{
    private readonly NtcaDbContext _db;
    public SiteModeService(NtcaDbContext db) => _db = db;

    public async Task<string> GetModeAsync(CancellationToken ct = default)
    {
        var row = await _db.SiteSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == "Site:Mode", ct);
        return string.IsNullOrWhiteSpace(row?.SettingValue) ? "Preview" : row.SettingValue;
    }

    public async Task<bool> IsPreviewAsync(CancellationToken ct = default)
        => !string.Equals(await GetModeAsync(ct), "Live", StringComparison.OrdinalIgnoreCase);

    public async Task SetModeAsync(string mode, CancellationToken ct = default)
    {
        var row = await _db.SiteSettings.FirstOrDefaultAsync(s => s.SettingKey == "Site:Mode", ct);
        if (row is null)
        {
            _db.SiteSettings.Add(new SiteSetting
            {
                SettingKey = "Site:Mode",
                SettingValue = mode,
                ValueType = "STRING",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.SettingValue = mode;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
