using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Ntca.Core;

namespace Ntca.Web.Services;

public sealed record WebsiteEnrichment(IReadOnlyList<string> Emails, IReadOnlyDictionary<string, string> Socials)
{
    public static WebsiteEnrichment Empty { get; } = new([], new Dictionary<string, string>());
}

public sealed class WebsiteEnrich
{
    public const string UserAgent = "NTCA Directory Bot/1.0";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    private static readonly Regex EmailRx = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HrefRx = new(
        @"href\s*=\s*[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] FallbackPaths = ["/contact", "/contact-us", "/about", "/about-us"];

    private static readonly (string Host, string Code)[] Hosts =
    [
        ("facebook.com", ShopSocialCatalog.Facebook),
        ("fb.com", ShopSocialCatalog.Facebook),
        ("instagram.com", ShopSocialCatalog.Instagram),
        ("tiktok.com", ShopSocialCatalog.TikTok),
        ("youtube.com", ShopSocialCatalog.YouTube),
        ("youtu.be", ShopSocialCatalog.YouTube),
        ("twitter.com", ShopSocialCatalog.X),
        ("x.com", ShopSocialCatalog.X),
        ("whatnot.com", ShopSocialCatalog.Whatnot),
        ("ebay.com", ShopSocialCatalog.Ebay)
    ];

    private readonly HttpClient _http;
    private readonly ILogger<WebsiteEnrich> _log;

    public WebsiteEnrich(HttpClient http, ILogger<WebsiteEnrich> log)
    {
        _http = http;
        _log = log;
        _http.Timeout = Timeout;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
    }

    public static WebsiteEnrichment Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return WebsiteEnrichment.Empty;
        var emails = new List<string>();
        foreach (Match m in EmailRx.Matches(html))
        {
            var addr = m.Value.Trim().TrimEnd('.', ',', ';');
            if (!IsUsableEmail(addr))
                continue;
            if (!emails.Contains(addr, StringComparer.OrdinalIgnoreCase))
                emails.Add(addr);
            if (emails.Count >= 5)
                break;
        }
        var socials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in HrefRx.Matches(html))
            TryAddSocial(socials, m.Groups[1].Value);
        return new WebsiteEnrichment(emails, socials);
    }

    public async Task<WebsiteEnrichment> FetchAsync(string? websiteUrl, CancellationToken ct)
    {
        if (!TryAbsolute(websiteUrl, out var home))
            return WebsiteEnrichment.Empty;
        var first = await GetHtmlAsync(home, ct);
        var parsed = Parse(first ?? "");
        if (parsed.Emails.Count > 0 && parsed.Socials.Count > 0)
            return parsed;
        foreach (var path in FallbackPaths)
        {
            if (!Uri.TryCreate(home, path, out var next))
                continue;
            var html = await GetHtmlAsync(next, ct);
            if (string.IsNullOrWhiteSpace(html))
                continue;
            var extra = Parse(html);
            parsed = Merge(parsed, extra);
            if (parsed.Emails.Count > 0 && parsed.Socials.Count > 0)
                break;
        }
        return parsed;
    }

    private async Task<string?> GetHtmlAsync(Uri url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
            if (!res.IsSuccessStatusCode)
                return null;
            var type = res.Content.Headers.ContentType?.MediaType ?? "";
            if (type.Length > 0 && !type.Contains("html", StringComparison.OrdinalIgnoreCase)
                && !type.Contains("text", StringComparison.OrdinalIgnoreCase))
                return null;
            var html = await res.Content.ReadAsStringAsync(ct);
            return html.Length > 600_000 ? html[..600_000] : html;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            _log.LogDebug(ex, "Website enrich skipped {Url}", url);
            return null;
        }
    }

    public static bool TryAbsolute(string? raw, out Uri uri)
    {
        uri = new Uri("https://invalid.invalid");
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var value = raw.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
            value = "https://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var created))
            return false;
        if (created.Scheme != Uri.UriSchemeHttp && created.Scheme != Uri.UriSchemeHttps)
            return false;
        uri = created;
        return true;
    }

    public static bool IsUsableEmail(string addr)
    {
        var lower = addr.ToLowerInvariant();
        if (lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".webp") || lower.EndsWith(".svg"))
            return false;
        if (lower.Contains("example.com") || lower.Contains("sentry.io") || lower.Contains("wixpress.com"))
            return false;
        if (lower.Contains("noreply") || lower.Contains("no-reply") || lower.Contains("donotreply"))
            return false;
        return true;
    }

    private static void TryAddSocial(Dictionary<string, string> socials, string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            && !Uri.TryCreate("https://" + raw.TrimStart('/'), UriKind.Absolute, out uri))
            return;
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];
        foreach (var (suffix, code) in Hosts)
        {
            if (host != suffix && !host.EndsWith("." + suffix, StringComparison.Ordinal))
                continue;
            if (code == ShopSocialCatalog.Ebay
                && !uri.AbsolutePath.StartsWith("/str/", StringComparison.OrdinalIgnoreCase)
                && !uri.AbsolutePath.StartsWith("/usr/", StringComparison.OrdinalIgnoreCase))
                continue;
            if (socials.ContainsKey(code))
                return;
            if (!ShopSocialNormalizer.TryNormalize(code, uri.ToString(), out var url, out _, out _))
                return;
            socials[code] = url;
            return;
        }
    }

    private static WebsiteEnrichment Merge(WebsiteEnrichment left, WebsiteEnrichment right)
    {
        var emails = left.Emails.ToList();
        foreach (var e in right.Emails)
        {
            if (!emails.Contains(e, StringComparer.OrdinalIgnoreCase))
                emails.Add(e);
        }
        var socials = new Dictionary<string, string>(left.Socials, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in right.Socials)
            socials.TryAdd(kv.Key, kv.Value);
        return new WebsiteEnrichment(emails, socials);
    }
}
