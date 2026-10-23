using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class ShopSocialNormalizer
{
    private static readonly Regex HandleChars = new(@"^[A-Za-z0-9._~+-]{1,80}$", RegexOptions.Compiled);
    private static readonly Regex BlueskyHandle = new(@"^[A-Za-z0-9.-]{2,80}$", RegexOptions.Compiled);
    private static readonly Regex HostLike = new(@"^[A-Za-z0-9.-]+\.[A-Za-z]{2,}(/.*)?$", RegexOptions.Compiled);

    public static bool TryNormalize(string platformCode, string? raw, out string url, out string? handle, out string? error)
        => TryNormalize(ShopSocialCatalog.Find(platformCode) ?? new SocialPlatform { Code = platformCode, Kind = "URL" }, raw, out url, out handle, out error);

    public static bool TryNormalize(SocialPlatform platform, string? raw, out string url, out string? handle, out string? error)
    {
        url = "";
        handle = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Enter a handle or URL.";
            return false;
        }

        var kind = (platform.Kind ?? "URL").Trim().ToUpperInvariant();
        return kind switch
        {
            "EMAIL" => TryEmail(raw.Trim(), out url, out handle, out error),
            "PHONE" => TryPhone(raw.Trim(), out url, out handle, out error),
            _ => TryHttp(platform, raw.Trim(), out url, out handle, out error)
        };
    }

    private static bool TryEmail(string raw, out string url, out string? handle, out string? error)
    {
        url = "";
        handle = null;
        error = null;
        var value = raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? raw[7..] : raw;
        try
        {
            var addr = new MailAddress(value);
            handle = addr.Address;
            url = "mailto:" + addr.Address;
            return true;
        }
        catch (FormatException)
        {
            error = "Enter a valid email address.";
            return false;
        }
    }

    private static bool TryPhone(string raw, out string url, out string? handle, out string? error)
    {
        url = "";
        handle = null;
        error = null;
        var value = raw.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ? raw[4..] : raw;
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (char.IsDigit(c) || (c == '+' && sb.Length == 0))
                sb.Append(c);
        }
        if (sb.Length < 7)
        {
            error = "Enter a phone number.";
            return false;
        }
        handle = sb.ToString();
        url = "tel:" + handle;
        return true;
    }

    private static bool TryHttp(SocialPlatform platform, string raw, out string url, out string? handle, out string? error)
    {
        url = "";
        handle = null;
        error = null;
        if (LooksLikeUrl(platform, raw))
            return FromUrl(platform, raw, out url, out handle, out error);
        return FromHandle(platform, raw, out url, out handle, out error);
    }

    private static bool LooksLikeUrl(SocialPlatform platform, string raw)
    {
        if (raw.Contains("://", StringComparison.Ordinal) || raw.StartsWith("//", StringComparison.Ordinal))
            return true;
        if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!HostLike.IsMatch(raw))
            return false;
        var host = StripWww(raw.Split('/', 2)[0]);
        if (string.Equals(platform.Code, ShopSocialCatalog.Website, StringComparison.OrdinalIgnoreCase))
            return true;
        if (HostAllowed(platform, host))
            return true;
        return raw.Contains('/', StringComparison.Ordinal);
    }

    private static bool FromHandle(SocialPlatform platform, string raw, out string url, out string? handle, out string? error)
    {
        url = "";
        handle = null;
        error = null;
        if (string.Equals(platform.Code, ShopSocialCatalog.Website, StringComparison.OrdinalIgnoreCase))
        {
            error = "Enter a full website URL.";
            return false;
        }

        var value = StripHandleDecor(platform, raw);
        if (!IsHandleOk(platform, value))
        {
            error = "That handle is not valid for " + platform.Label + ".";
            return false;
        }
        if (string.IsNullOrWhiteSpace(platform.UrlPattern) || !platform.UrlPattern.Contains("{handle}", StringComparison.OrdinalIgnoreCase))
        {
            error = "This platform needs a full URL.";
            return false;
        }
        handle = value;
        url = platform.UrlPattern.Replace("{handle}", value, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool FromUrl(SocialPlatform platform, string raw, out string url, out string? handle, out string? error)
    {
        url = "";
        handle = null;
        error = null;
        var candidate = raw;
        if (candidate.StartsWith("//", StringComparison.Ordinal))
            candidate = "https:" + candidate;
        if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = "https://" + candidate;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "Enter an http or https URL.";
            return false;
        }

        var host = StripWww(uri.Host);
        if (!HostAllowed(platform, host))
        {
            error = platform.Label + " links must be on " + platform.HostAllowlist + ".";
            return false;
        }

        handle = ExtractHandle(platform, uri);
        if (CanRebuild(platform, handle) && !KeepOriginalPath(platform, uri))
        {
            url = platform.UrlPattern!.Replace("{handle}", handle, StringComparison.OrdinalIgnoreCase);
            return true;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = "https",
            Port = -1,
            Fragment = ""
        };
        var originalHadWww = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
        if (originalHadWww || PreferWww(platform))
            builder.Host = "www." + host;
        else
            builder.Host = host;
        builder.Query = StripTracking(uri.Query);
        url = builder.Uri.AbsoluteUri;
        if (url.EndsWith('/') && string.IsNullOrEmpty(uri.Query) && uri.AbsolutePath == "/")
            url = url.TrimEnd('/');
        return true;
    }

    private static bool HostAllowed(SocialPlatform platform, string host)
    {
        if (string.IsNullOrWhiteSpace(platform.HostAllowlist))
            return true;
        foreach (var raw in platform.HostAllowlist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var allowed = StripWww(raw).TrimStart('.');
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool PreferWww(SocialPlatform platform)
        => platform.UrlPattern != null
           && platform.UrlPattern.Contains("://www.", StringComparison.OrdinalIgnoreCase);

    private static bool CanRebuild(SocialPlatform platform, string? handle)
        => !string.Equals(platform.Code, ShopSocialCatalog.Website, StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(handle)
           && !string.IsNullOrWhiteSpace(platform.UrlPattern)
           && platform.UrlPattern.Contains("{handle}", StringComparison.OrdinalIgnoreCase)
           && IsHandleOk(platform, handle);

    private static bool KeepOriginalPath(SocialPlatform platform, Uri uri)
    {
        var path = uri.AbsolutePath.Trim('/').ToLowerInvariant();
        if (string.Equals(platform.Code, ShopSocialCatalog.Facebook, StringComparison.OrdinalIgnoreCase)
            && (path.StartsWith("pages/", StringComparison.Ordinal) || path.StartsWith("profile.php", StringComparison.Ordinal)
                || uri.Query.Contains("id=", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (string.Equals(platform.Code, ShopSocialCatalog.YouTube, StringComparison.OrdinalIgnoreCase)
            && (path.StartsWith("channel/", StringComparison.Ordinal) || path.StartsWith("c/", StringComparison.Ordinal)
                || path.StartsWith("user/", StringComparison.Ordinal)))
            return true;
        if (string.Equals(platform.Code, ShopSocialCatalog.LinkedIn, StringComparison.OrdinalIgnoreCase)
            && path.StartsWith("in/", StringComparison.Ordinal))
            return true;
        if (string.Equals(platform.Code, ShopSocialCatalog.Reddit, StringComparison.OrdinalIgnoreCase)
            && (path.StartsWith("r/", StringComparison.Ordinal)))
            return true;
        return false;
    }

    private static string? ExtractHandle(SocialPlatform platform, Uri uri)
    {
        var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length == 0)
            return null;
        var code = platform.Code.ToUpperInvariant();
        var first = segs[0];
        var last = segs[^1];
        return code switch
        {
            ShopSocialCatalog.Instagram or ShopSocialCatalog.Pinterest or ShopSocialCatalog.Twitch
                or ShopSocialCatalog.X or ShopSocialCatalog.Snapchat or ShopSocialCatalog.Linktree
                or ShopSocialCatalog.Facebook
                => StripAt(first),
            ShopSocialCatalog.TikTok or ShopSocialCatalog.Threads
                => StripAt(first),
            ShopSocialCatalog.YouTube => first.Equals("channel", StringComparison.OrdinalIgnoreCase)
                || first.Equals("c", StringComparison.OrdinalIgnoreCase)
                || first.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? last
                    : StripAt(first),
            ShopSocialCatalog.Whatnot => first.Equals("user", StringComparison.OrdinalIgnoreCase) ? last : StripAt(first),
            ShopSocialCatalog.Bluesky => first.Equals("profile", StringComparison.OrdinalIgnoreCase) ? last : last,
            ShopSocialCatalog.Discord => first.Equals("invite", StringComparison.OrdinalIgnoreCase) ? last : last,
            ShopSocialCatalog.Ebay => first is "str" or "usr" or "sch" ? last : last,
            ShopSocialCatalog.Reddit => segs.Length >= 2 ? segs[1] : last,
            ShopSocialCatalog.LinkedIn => segs.Length >= 2 ? segs[1] : last,
            _ => StripAt(last)
        };
    }

    private static string StripHandleDecor(SocialPlatform platform, string raw)
    {
        var value = raw.Trim();
        if (value.StartsWith('@'))
            value = value[1..];
        var prefix = platform.HandlePrefix;
        if (!string.IsNullOrEmpty(prefix) && prefix != "@"
            && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            value = value[prefix.Length..];
        return value.Trim().Trim('/');
    }

    private static string StripAt(string value)
        => value.StartsWith('@') ? value[1..] : value;

    private static bool IsHandleOk(SocialPlatform platform, string? handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
            return false;
        if (handle.Contains('/') || handle.Contains(' ') || handle.Contains('?'))
            return false;
        if (string.Equals(platform.Code, ShopSocialCatalog.Bluesky, StringComparison.OrdinalIgnoreCase))
            return BlueskyHandle.IsMatch(handle);
        return HandleChars.IsMatch(handle);
    }

    private static string StripWww(string host)
        => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private static string StripTracking(string query)
    {
        if (string.IsNullOrEmpty(query))
            return "";
        var keep = new List<string>();
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = part.Split('=', 2)[0];
            if (key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                || key.Equals("fbclid", StringComparison.OrdinalIgnoreCase)
                || key.Equals("igshid", StringComparison.OrdinalIgnoreCase)
                || key.Equals("si", StringComparison.OrdinalIgnoreCase))
                continue;
            keep.Add(part);
        }
        return keep.Count == 0 ? "" : "?" + string.Join("&", keep);
    }
}
