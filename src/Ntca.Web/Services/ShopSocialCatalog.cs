using Ntca.Core;

namespace Ntca.Web.Services;

public static class ShopSocialCatalog
{
    public const string Website = "WEBSITE";
    public const string Facebook = "FACEBOOK";
    public const string Instagram = "INSTAGRAM";
    public const string TikTok = "TIKTOK";
    public const string YouTube = "YOUTUBE";
    public const string Whatnot = "WHATNOT";
    public const string X = "X";
    public const string Threads = "THREADS";
    public const string Bluesky = "BLUESKY";
    public const string Discord = "DISCORD";
    public const string Ebay = "EBAY";
    public const string Twitch = "TWITCH";
    public const string Reddit = "REDDIT";
    public const string LinkedIn = "LINKEDIN";
    public const string Pinterest = "PINTEREST";
    public const string Snapchat = "SNAPCHAT";
    public const string Linktree = "LINKTREE";
    public const string Email = "EMAIL";
    public const string Phone = "PHONE";

    public static readonly IReadOnlyList<string> DisplayOrder =
    [
        Website, Facebook, Instagram, TikTok, YouTube, Whatnot,
        X, Threads, Bluesky, Discord, Ebay, Twitch, Reddit, LinkedIn,
        Pinterest, Snapchat, Linktree, Email, Phone
    ];

    public static IReadOnlyList<SocialPlatform> BuiltIn { get; } = Build();

    public static SocialPlatform? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        return BuiltIn.FirstOrDefault(p => p.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsCustomIcon(string? code)
        => string.Equals(code, Whatnot, StringComparison.OrdinalIgnoreCase);

    public static bool IsSameAs(string? kind, string? url)
    {
        if (string.Equals(kind, "EMAIL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(kind, "PHONE", StringComparison.OrdinalIgnoreCase))
            return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var u)
               && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
    }

    public static string TrackHref(long shopId, string platformCode)
    {
        if (string.Equals(platformCode, Website, StringComparison.OrdinalIgnoreCase))
            return "/t/website?shop=" + shopId;
        return "/t/social?shop=" + shopId + "&net=" + Uri.EscapeDataString(platformCode);
    }

    private static IReadOnlyList<SocialPlatform> Build() =>
    [
        P(Website, "Website", "fa-solid fa-globe", "https://{handle}", null, 10, "URL"),
        P(Facebook, "Facebook", "fa-brands fa-facebook", "https://www.facebook.com/{handle}", "facebook.com,fb.com", 20, "URL"),
        P(Instagram, "Instagram", "fa-brands fa-instagram", "https://www.instagram.com/{handle}/", "instagram.com", 30, "URL", "@"),
        P(TikTok, "TikTok", "fa-brands fa-tiktok", "https://www.tiktok.com/@{handle}", "tiktok.com", 40, "URL", "@"),
        P(YouTube, "YouTube", "fa-brands fa-youtube", "https://www.youtube.com/@{handle}", "youtube.com,youtu.be", 50, "URL", "@"),
        P(Whatnot, "Whatnot", null, "https://www.whatnot.com/user/{handle}", "whatnot.com", 60, "URL"),
        P(X, "X", "fa-brands fa-x-twitter", "https://x.com/{handle}", "x.com,twitter.com", 70, "URL", "@"),
        P(Threads, "Threads", "fa-brands fa-threads", "https://www.threads.net/@{handle}", "threads.net", 80, "URL", "@"),
        P(Bluesky, "Bluesky", "fa-brands fa-bluesky", "https://bsky.app/profile/{handle}", "bsky.app", 90, "URL", "@"),
        P(Discord, "Discord", "fa-brands fa-discord", "https://discord.gg/{handle}", "discord.gg,discord.com,discordapp.com", 100, "URL"),
        P(Ebay, "eBay store", "fa-brands fa-ebay", "https://www.ebay.com/str/{handle}", "ebay.com", 110, "URL"),
        P(Twitch, "Twitch", "fa-brands fa-twitch", "https://www.twitch.tv/{handle}", "twitch.tv", 120, "URL"),
        P(Reddit, "Reddit", "fa-brands fa-reddit", "https://www.reddit.com/user/{handle}", "reddit.com", 130, "URL", "u/"),
        P(LinkedIn, "LinkedIn", "fa-brands fa-linkedin", "https://www.linkedin.com/company/{handle}", "linkedin.com", 140, "URL"),
        P(Pinterest, "Pinterest", "fa-brands fa-pinterest", "https://www.pinterest.com/{handle}/", "pinterest.com", 150, "URL"),
        P(Snapchat, "Snapchat", "fa-brands fa-snapchat", "https://www.snapchat.com/add/{handle}", "snapchat.com", 160, "URL"),
        P(Linktree, "Linktree", "fa-solid fa-link", "https://linktr.ee/{handle}", "linktr.ee", 170, "URL"),
        P(Email, "Email", "fa-solid fa-envelope", "mailto:{handle}", null, 180, "EMAIL"),
        P(Phone, "Phone/SMS", "fa-solid fa-comment-sms", "tel:{handle}", null, 190, "PHONE")
    ];

    private static SocialPlatform P(
        string code, string label, string? icon, string? pattern, string? hosts, int order, string kind, string? prefix = null)
        => new()
        {
            Code = code,
            Label = label,
            IconClass = icon,
            UrlPattern = pattern,
            HostAllowlist = hosts,
            HandlePrefix = prefix,
            DisplayOrder = order,
            IsActive = true,
            Kind = kind
        };
}
