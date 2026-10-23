using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;

namespace Ntca.Web.Services;

public static class ImpressionSurfaces
{
    public const string DirectoryList = "DIRECTORY_LIST";
    public const string DirectorySearch = "DIRECTORY_SEARCH";
    public const string DirectoryState = "DIRECTORY_STATE";
    public const string HomeRotation = "HOME_ROTATION";
    public const string Featured = "FEATURED";
    public const string ShowExhibitor = "SHOW_EXHIBITOR";
    public const string MapPin = "MAP_PIN";
    public const string ApiApp = "API_APP";
    public const string Detail = "DETAIL";
    public const string ClickWebsite = "CLICK_WEBSITE";
    public const string ClickPhone = "CLICK_PHONE";
    public const string ClickDirections = "CLICK_DIRECTIONS";
    public const string ClickSocial = "CLICK_SOCIAL";
}

public readonly record struct ImpressionHit(
    long ShopId,
    string Surface,
    short? Position,
    string? PageUrl,
    byte[]? SessionHash,
    bool IsBot,
    DateTime CreatedAt);

public sealed class SessionSaltCache
{
    private readonly IServiceScopeFactory _scopes;
    private readonly object _gate = new();
    private DateTime _date;
    private byte[] _salt = [];

    public SessionSaltCache(IServiceScopeFactory scopes) => _scopes = scopes;

    public byte[] GetToday()
    {
        var today = DateTime.UtcNow.Date;
        lock (_gate)
        {
            if (_date == today && _salt.Length == 32)
                return _salt;
        }
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NtcaDbContext>();
        var row = db.SessionSalts.AsNoTracking().FirstOrDefault(s => s.SaltDate == today);
        byte[] salt;
        if (row is not null)
        {
            salt = row.Salt;
        }
        else
        {
            salt = RandomNumberGenerator.GetBytes(32);
            try
            {
                db.SessionSalts.Add(new SessionSalt { SaltDate = today, Salt = salt, CreatedAt = DateTime.UtcNow });
                db.SaveChanges();
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var again = db.SessionSalts.AsNoTracking().FirstOrDefault(s => s.SaltDate == today);
                if (again is not null)
                    salt = again.Salt;
            }
        }
        lock (_gate)
        {
            _date = today;
            _salt = salt;
        }
        return salt;
    }
}

public sealed class ImpressionBuffer
{
    private readonly ConcurrentQueue<ImpressionHit> _q = new();
    public int Count => _q.Count;

    public void Enqueue(ImpressionHit hit) => _q.Enqueue(hit);

    public List<ImpressionHit> Drain(int max = 400)
    {
        var list = new List<ImpressionHit>(Math.Min(max, _q.Count));
        while (list.Count < max && _q.TryDequeue(out var hit))
            list.Add(hit);
        return list;
    }
}

public interface IImpressionTracker
{
    void Record(long shopId, string surface, int? position = null, string? pageUrl = null);
    void RecordMany(IReadOnlyList<Shop> shops, string surface, string? pageUrl = null);
}

public sealed class ImpressionTracker : IImpressionTracker
{
    public const string CookieName = "ntca.sid";
    private static readonly string[] BotTokens =
    [
        "bot", "crawl", "spider", "slurp", "bingpreview", "facebookexternalhit", "bytespider",
        "gptbot", "claudebot", "anthropic", "chatgpt", "perplexity", "ccbot", "ahrefs",
        "semrush", "mj12", "dotbot", "petalbot", "yandex", "baidu", "duckduck", "applebot",
        "twitterbot", "linkedinbot", "slackbot", "discordbot", "whatsapp", "telegrambot",
        "preview", "headless", "lighthouse", "pagespeed", "pingdom", "uptimerobot",
        "wget", "curl/", "python-requests", "go-http-client", "httpclient"
    ];

    private readonly ImpressionBuffer _buffer;
    private readonly IHttpContextAccessor _http;
    private readonly SessionSaltCache _salts;

    public ImpressionTracker(ImpressionBuffer buffer, IHttpContextAccessor http, SessionSaltCache salts)
    {
        _buffer = buffer;
        _http = http;
        _salts = salts;
    }

    public void Record(long shopId, string surface, int? position = null, string? pageUrl = null)
    {
        var ctx = _http.HttpContext;
        var ua = ctx?.Request.Headers.UserAgent.ToString() ?? "";
        _buffer.Enqueue(new ImpressionHit(
            shopId,
            surface,
            position is >= 0 and <= short.MaxValue ? (short)position.Value : null,
            Truncate(pageUrl ?? PageUrl(ctx), 400),
            SessionHash(ctx),
            IsBot(ua),
            DateTime.UtcNow));
    }

    public void RecordMany(IReadOnlyList<Shop> shops, string surface, string? pageUrl = null)
    {
        for (var i = 0; i < shops.Count; i++)
            Record(shops[i].ShopId, surface, i + 1, pageUrl);
    }

    public static bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return true;
        var ua = userAgent.ToLowerInvariant();
        foreach (var token in BotTokens)
        {
            if (ua.Contains(token, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private byte[]? SessionHash(HttpContext? ctx)
    {
        if (ctx is null)
            return null;
        string sid;
        if (ctx.Items[CookieName] is string cached && cached.Length >= 8)
            sid = cached;
        else if (!ctx.Request.Cookies.TryGetValue(CookieName, out var existing) || string.IsNullOrEmpty(existing) || existing.Length < 8)
        {
            sid = Guid.NewGuid().ToString("N");
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.Cookies.Append(CookieName, sid, new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = ctx.Request.IsHttps,
                    MaxAge = TimeSpan.FromDays(400)
                });
            }
        }
        else
            sid = existing;
        ctx.Items[CookieName] = sid;
        var salt = _salts.GetToday();
        var payload = Encoding.UTF8.GetBytes(sid + ":" + DateTime.UtcNow.ToString("yyyy-MM-dd"));
        var buf = new byte[payload.Length + salt.Length];
        Buffer.BlockCopy(payload, 0, buf, 0, payload.Length);
        Buffer.BlockCopy(salt, 0, buf, payload.Length, salt.Length);
        return SHA256.HashData(buf);
    }

    private static string? PageUrl(HttpContext? ctx)
    {
        if (ctx is null)
            return null;
        return ctx.Request.Path.Value + ctx.Request.QueryString.Value;
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : (value.Length <= max ? value : value[..max]);
}

public sealed class ImpressionFlushService : BackgroundService
{
    private readonly ImpressionBuffer _buffer;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ImpressionFlushService> _log;

    public ImpressionFlushService(ImpressionBuffer buffer, IServiceScopeFactory scopes, ILogger<ImpressionFlushService> log)
    {
        _buffer = buffer;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            await FlushAsync(CancellationToken.None);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await FlushAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var batch = _buffer.Drain();
        if (batch.Count == 0)
            return;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NtcaDbContext>();
            db.ShopImpressions.AddRange(batch.Select(h => new ShopImpression
            {
                ShopId = h.ShopId,
                Surface = h.Surface,
                Position = h.Position,
                PageUrl = h.PageUrl,
                SessionHash = h.SessionHash,
                IsBot = h.IsBot,
                CreatedAt = h.CreatedAt
            }));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to flush {Count} shop impressions", batch.Count);
            foreach (var hit in batch)
                _buffer.Enqueue(hit);
        }
    }
}

public sealed class ImpressionRollupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ImpressionRollupService> _log;

    public ImpressionRollupService(IServiceScopeFactory scopes, ILogger<ImpressionRollupService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        await RollupAsync(stoppingToken);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RollupAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task RollupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NtcaDbContext>();
            await db.Database.ExecuteSqlRawAsync("EXEC analytics.usp_RollupShopDailyStats", ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Shop daily-stat rollup skipped");
        }
    }
}

public sealed class ShopStatTotals
{
    public int Impressions { get; set; }
    public int Views { get; set; }
    public int Clicks { get; set; }
    public int WebsiteClicks { get; set; }
    public int PhoneClicks { get; set; }
    public int DirectionsClicks { get; set; }
    public int SocialClicks { get; set; }
}

public sealed class ShopDayPoint
{
    public DateTime Date { get; set; }
    public int Impressions { get; set; }
    public int Views { get; set; }
    public int Clicks { get; set; }
}

public static class ShopStatQueries
{
    public static async Task<ShopStatTotals> TotalsAsync(NtcaDbContext db, long? shopId, int days, CancellationToken ct)
    {
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var q = db.ShopImpressions.AsNoTracking().Where(i => !i.IsBot && i.CreatedAt >= from);
        if (shopId is long id)
            q = q.Where(i => i.ShopId == id);
        var rows = await q.GroupBy(i => i.Surface).Select(g => new { Surface = g.Key, N = g.Count() }).ToListAsync(ct);
        var t = new ShopStatTotals();
        foreach (var r in rows)
        {
            if (r.Surface == ImpressionSurfaces.Detail)
                t.Views += r.N;
            else if (r.Surface.StartsWith("CLICK_", StringComparison.Ordinal))
            {
                t.Clicks += r.N;
                if (r.Surface == ImpressionSurfaces.ClickWebsite) t.WebsiteClicks += r.N;
                else if (r.Surface == ImpressionSurfaces.ClickPhone) t.PhoneClicks += r.N;
                else if (r.Surface == ImpressionSurfaces.ClickDirections) t.DirectionsClicks += r.N;
                else if (r.Surface == ImpressionSurfaces.ClickSocial) t.SocialClicks += r.N;
            }
            else
                t.Impressions += r.N;
        }
        return t;
    }

    public static async Task<List<ShopDayPoint>> DailyAsync(NtcaDbContext db, long shopId, int days, CancellationToken ct)
    {
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var rows = await db.ShopImpressions.AsNoTracking()
            .Where(i => !i.IsBot && i.ShopId == shopId && i.CreatedAt >= from)
            .ToListAsync(ct);
        var map = Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .ToDictionary(d => d, d => new ShopDayPoint { Date = d });
        foreach (var i in rows)
        {
            var day = i.CreatedAt.Date;
            if (!map.TryGetValue(day, out var p))
                continue;
            if (i.Surface == ImpressionSurfaces.Detail) p.Views++;
            else if (i.Surface.StartsWith("CLICK_", StringComparison.Ordinal)) p.Clicks++;
            else p.Impressions++;
        }
        return map.Values.OrderBy(p => p.Date).ToList();
    }
}
