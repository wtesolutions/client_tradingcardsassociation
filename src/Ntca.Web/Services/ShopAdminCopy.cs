using Ntca.Core;

namespace Ntca.Web.Services;

public static class ShopAdminCopy
{
    public static string TitleCaseStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Unknown";
        var t = raw.Trim().Replace('_', ' ').ToLowerInvariant();
        return string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    public static string RawSource(Shop shop)
    {
        var parts = new[] { shop.SourceSystem, shop.Source, shop.SourceRef }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(" · ", parts);
        return string.IsNullOrWhiteSpace(joined) ? "" : joined;
    }

    public static string SourceLabel(Shop shop)
    {
        var raw = RawSource(shop);
        if (string.IsNullOrWhiteSpace(raw))
            return "Unknown source";
        if (shop.IsDestinationShop)
            return DestinationReviewLabel;
        return raw;
    }

    public const string DestinationReviewLabel = "Destination — imported, review needed";

    public static string DesignationLabel(Shop shop)
    {
        if (shop.IsEditorialFeatured)
            return "Featured";
        if (shop.IsDestinationShop)
            return DestinationReviewLabel;
        return "None";
    }

    public static string DesignationReviewState(Shop shop)
    {
        if (shop.IsEditorialFeatured)
            return "Editorial Featured (not paid / Sponsored).";
        if (shop.IsDestinationShop)
            return DestinationReviewLabel + ". Not published as a paid designation.";
        return "No designation.";
    }

    public static bool Locks(Shop shop, string field)
        => (shop.OwnerFieldLocks ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(field, StringComparer.OrdinalIgnoreCase);

    public static void AddLock(Shop shop, string field)
    {
        var set = new HashSet<string>((shop.OwnerFieldLocks ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase)
        { field };
        shop.OwnerFieldLocks = string.Join(',', set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    public static string FunnelStepLabel(string key) => key switch
    {
        "register" => "Registration",
        "thanks" => "Thank-you page",
        "confirm" => "Confirmation",
        "who" => "About you",
        "zip" => "ZIP code",
        "interests" => "Interests",
        "experience" => "Experience",
        "travel" => "Travel distance",
        "shop_find" => "Find your shop",
        "shop_tier" => "Listing tier",
        "show_find" => "Find your show",
        "mfr_find" => "Find your brand",
        "done" => "Onboarding done",
        _ => TitleCaseStatus(key)
    };

    public static string DashIfZero(int n) => n == 0 ? "—" : n.ToString("N0");

    public static bool EmailDomainMatchesWebsite(string email, string? website)
    {
        var at = email.IndexOf('@');
        if (at < 1 || at == email.Length - 1)
            return false;
        var domain = email[(at + 1)..].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(website))
            return false;
        var raw = website.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal))
            raw = "https://" + raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return false;
        var host = uri.Host.Trim().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];
        return host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
    }
}
