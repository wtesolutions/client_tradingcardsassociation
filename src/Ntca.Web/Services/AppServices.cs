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
    private readonly ISmtpTransport _smtp;

    public CompositeEmailSender(
        IOptions<SocketLabsOptions> socket,
        IOptions<EmailOptions> email,
        IWebHostEnvironment env,
        ILogger<CompositeEmailSender> log,
        ISmtpTransport smtp)
    {
        _socket = socket.Value;
        _email = email.Value;
        _env = env;
        _log = log;
        _smtp = smtp;
    }

    public bool InjectionConfigured =>
        _socket.ServerId > 0 && !string.IsNullOrWhiteSpace(_socket.ApiKey);

    public bool IsConfigured => _email.IsSmtpConfigured || InjectionConfigured;

    public async Task<EmailSendResult> SendAsync(string toEmail, string subject, string textBody, string? htmlBody = null, CancellationToken ct = default)
    {
        if (_email.IsSmtpConfigured)
            return await SendSmtpAsync(toEmail, subject, textBody, htmlBody, ct);
        if (InjectionConfigured)
            return await SendInjectionAsync(toEmail, subject, textBody, htmlBody, ct);

        var pickup = await WriteDevMailAsync(toEmail, subject, textBody, ct);
        _log.LogWarning(
            "Email is not configured (no SMTP host, no SocketLabs Injection API keys) — no email was sent. Wrote {File} to {To} subject {Subject}.",
            pickup, toEmail, subject);
        if (_env.IsDevelopment())
            _log.LogWarning("DEV email body to {To}:\n{Body}", toEmail, textBody);
        return new EmailSendResult(false, "dev-file",
            "Email is not configured (no SMTP host and no SocketLabs Injection API keys), so no email was sent. The message (including the code) is in the console and in " + pickup + ".",
            pickup);
    }

    private async Task<EmailSendResult> SendSmtpAsync(string toEmail, string subject, string textBody, string? htmlBody, CancellationToken ct)
    {
        try
        {
            await _smtp.SendAsync(_email, toEmail, subject, textBody, htmlBody, ct);
            _log.LogInformation(
                "SMTP send Success to {To} subject {Subject} via {Host}:{Port} from {From}",
                toEmail, subject, _email.SmtpHost, _email.SmtpPort > 0 ? _email.SmtpPort : 587, _email.FromAddress);
            return new EmailSendResult(true, "smtp", "Sign-in code emailed.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SMTP send failed to {To} via {Host}", toEmail, _email.SmtpHost);
            var failedPickup = await WriteDevMailAsync(toEmail, subject, textBody, ct);
            return new EmailSendResult(false, "smtp-error",
                "SMTP did not send the email (" + ex.Message + "). A copy is in " + failedPickup + " and the console.",
                failedPickup);
        }
    }

    private async Task<EmailSendResult> SendInjectionAsync(string toEmail, string subject, string textBody, string? htmlBody, CancellationToken ct)
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
                "SocketLabs Injection send {Result} to {To} subject {Subject} (from {From}, serverId set={HasServer})",
                response.Result, toEmail, subject, _email.FromAddress, _socket.ServerId > 0);
            if (response.Result == SendResult.Success)
                return new EmailSendResult(true, "socketlabs", "Sign-in code emailed.");

            var err = response.Result + (string.IsNullOrWhiteSpace(response.ResponseMessage) ? "" : ": " + response.ResponseMessage);
            _log.LogWarning("SocketLabs Injection send did not succeed: {Result} {Error}", response.Result, response.ResponseMessage);
            var failedPickup = await WriteDevMailAsync(toEmail, subject, textBody, ct);
            return new EmailSendResult(false, "socketlabs-failed",
                "SocketLabs Injection API did not send the email (" + err + "). A copy is in " + failedPickup + " and the console.",
                failedPickup);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SocketLabs Injection send failed to {To} subject {Subject}", toEmail, subject);
            var failedPickup = await WriteDevMailAsync(toEmail, subject, textBody, ct);
            return new EmailSendResult(false, "socketlabs-error",
                "SocketLabs Injection API threw: " + ex.Message + ". A copy is in " + failedPickup + " and the console.",
                failedPickup);
        }
    }

    public static string DevMailFolder(string contentRoot)
        => Path.Combine(contentRoot, "App_Data", "dev-mail");

    private async Task<string> WriteDevMailAsync(string toEmail, string subject, string textBody, CancellationToken ct)
    {
        var pickup = DevMailFolder(_env.ContentRootPath);
        Directory.CreateDirectory(pickup);
        var file = Path.Combine(pickup, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.txt");
        var payload = $"From: {_email.FromName} <{_email.FromAddress}>\nTo: {toEmail}\nSubject: {subject}\n\n{textBody}\n";
        await File.WriteAllTextAsync(file, payload, ct);
        return file;
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

    public bool UsesObjectStorage => false;

    public string PublicUrl(string relativePath)
    {
        var key = MediaPaths.ObjectKey(relativePath);
        if (!string.IsNullOrWhiteSpace(_opt.PublicBaseUrl))
            return _opt.PublicBaseUrl.TrimEnd('/') + "/" + key.TrimStart('/');
        return "/media/" + key.TrimStart('/');
    }

    public async Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken ct = default)
    {
        var full = ResolveUpload(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return PublicUrl(relativePath);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
        => Task.FromResult(ResolveExisting(relativePath) is not null);

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var full = ResolveUpload(relativePath);
        if (File.Exists(full))
            File.Delete(full);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var full = ResolveExisting(relativePath);
        if (full is null)
            return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(full));
    }

    private string ResolveUpload(string relativePath)
    {
        var key = MediaPaths.ObjectKey(relativePath);
        return Path.GetFullPath(Path.Combine(UploadRoot(), key.Replace('/', Path.DirectorySeparatorChar)));
    }

    private string? ResolveExisting(string relativePath)
    {
        var key = MediaPaths.ObjectKey(relativePath);
        var upload = Path.GetFullPath(Path.Combine(UploadRoot(), key.Replace('/', Path.DirectorySeparatorChar)));
        if (File.Exists(upload))
            return upload;
        var legacyRel = MediaPaths.LegacyRelative(key).Replace('/', Path.DirectorySeparatorChar);
        foreach (var root in LegacyRoots())
        {
            var candidate = Path.GetFullPath(Path.Combine(root, legacyRel));
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private string UploadRoot()
        => Path.IsPathRooted(_opt.LocalRoot)
            ? _opt.LocalRoot
            : Path.Combine(_env.ContentRootPath, _opt.LocalRoot);

    private IEnumerable<string> LegacyRoots()
    {
        if (!string.IsNullOrWhiteSpace(_opt.LegacyRoot))
            yield return Path.IsPathRooted(_opt.LegacyRoot)
                ? _opt.LegacyRoot
                : Path.GetFullPath(Path.Combine(_env.ContentRootPath, _opt.LegacyRoot));
        yield return Path.GetFullPath(Path.Combine(_env.ContentRootPath, "legacy-site"));
        yield return Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "..", "legacy-site"));
    }
}

public sealed class LoginCodeService
{
    private readonly NtcaDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly IWebHostEnvironment _env;
    private readonly AuthOptions _auth;
    private readonly ILogger<LoginCodeService> _log;

    public LoginCodeService(
        NtcaDbContext db,
        IAppEmailSender email,
        IWebHostEnvironment env,
        IOptions<AuthOptions> auth,
        ILogger<LoginCodeService> log)
    {
        _db = db;
        _email = email;
        _env = env;
        _auth = auth.Value;
        _log = log;
    }

    public bool BypassEnabled => DevLoginBypass.IsEnabled(_env, _auth);

    public static byte[] HashCode(string email, string code)
    {
        var material = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant() + ":" + code.Trim());
        return SHA256.HashData(material);
    }

    public async Task<EmailSendResult> IssueAsync(string email, string purpose, long? userId, CancellationToken ct = default)
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
        return await _email.SendAsync(email, subject, body, ct: ct);
    }

    public async Task<bool> VerifyAsync(string email, string purpose, string code, CancellationToken ct = default)
    {
        if (DevLoginBypass.Accepts(_env, _auth, code))
        {
            _log.LogWarning("DEV bypass code accepted for {Email} ({Purpose})", email, purpose);
            await WriteBypassAuditAsync(email, purpose, ct);
            return true;
        }

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

    private async Task WriteBypassAuditAsync(string email, string purpose, CancellationToken ct)
    {
        try
        {
            var userId = await _db.Users.AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => (long?)u.UserId)
                .FirstOrDefaultAsync(ct);
            _db.AuditLogs.Add(new AuditLog
            {
                EntityType = "AppUser",
                EntityId = userId ?? 0,
                Action = "DEV_BYPASS_LOGIN",
                UserId = userId,
                ChangesJson = "{\"purpose\":\"" + purpose.Replace("\"", "") + "\",\"email\":\"" + email.Replace("\"", "") + "\"}",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to write DEV bypass audit row for {Email}", email);
        }
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
