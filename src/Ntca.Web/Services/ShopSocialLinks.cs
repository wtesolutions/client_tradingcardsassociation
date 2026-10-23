using Ntca.Core;

namespace Ntca.Web.Services;

public sealed class ShopSocialLink
{
    public long ShopSocialId { get; init; }
    public required string PlatformCode { get; init; }
    public required string Label { get; init; }
    public string? IconClass { get; init; }
    public required string Url { get; init; }
    public string? Handle { get; init; }
    public required string TrackHref { get; init; }
    public int SortOrder { get; init; }
    public bool Verified { get; init; }
    public string Source { get; init; } = "seed";
    public bool CustomIcon => ShopSocialCatalog.IsCustomIcon(PlatformCode);
    public bool IsSameAs => ShopSocialCatalog.IsSameAs(Kind, Url);
    public string Kind { get; init; } = "URL";
}

public static class ShopSocialLinks
{
    public static IReadOnlyList<ShopSocialLink> Resolve(Shop shop, int take = 0)
    {
        var fromRows = FromRows(shop.ShopId, shop.Socials);
        var links = fromRows.Count > 0 ? fromRows : FromLegacy(shop);
        return take > 0 ? links.Take(take).ToList() : links;
    }

    public static IReadOnlyList<ShopSocialLink> FromRows(long shopId, IEnumerable<ShopSocial>? rows)
    {
        if (rows is null)
            return [];
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .Select(r =>
            {
                var platform = r.Platform ?? ShopSocialCatalog.Find(r.PlatformCode);
                return new ShopSocialLink
                {
                    ShopSocialId = r.ShopSocialId,
                    PlatformCode = r.PlatformCode,
                    Label = platform?.Label ?? r.PlatformCode,
                    IconClass = platform?.IconClass,
                    Url = r.Url,
                    Handle = r.Handle,
                    TrackHref = ShopSocialCatalog.TrackHref(shopId, r.PlatformCode),
                    SortOrder = r.SortOrder,
                    Verified = r.Verified,
                    Source = r.Source,
                    Kind = platform?.Kind ?? "URL"
                };
            })
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ShopSocialLink> FromLegacy(Shop shop)
    {
        var links = new List<ShopSocialLink>();
        AddLegacy(links, shop, ShopSocialCatalog.Website, shop.WebsiteUrl);
        AddLegacy(links, shop, ShopSocialCatalog.Facebook, shop.FacebookUrl);
        if (!string.IsNullOrWhiteSpace(shop.InstagramHandle))
        {
            var raw = shop.InstagramHandle;
            if (ShopSocialNormalizer.TryNormalize(ShopSocialCatalog.Instagram, raw, out var url, out var handle, out _))
                AddBuilt(links, shop.ShopId, ShopSocialCatalog.Instagram, url, handle);
            else
                AddBuilt(links, shop.ShopId, ShopSocialCatalog.Instagram, ShopCopy.InstagramUrl(raw), raw.Trim().TrimStart('@'));
        }
        return links;
    }

    public static IReadOnlyList<string> SameAs(Shop shop)
        => Resolve(shop).Where(l => l.IsSameAs).Select(l => l.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static string? Destination(Shop shop, string kind, string? net)
    {
        var links = Resolve(shop);
        if (string.Equals(kind, "website", StringComparison.OrdinalIgnoreCase))
            return links.FirstOrDefault(l => l.PlatformCode.Equals(ShopSocialCatalog.Website, StringComparison.OrdinalIgnoreCase))?.Url
                   ?? shop.WebsiteUrl;
        if (!string.Equals(kind, "social", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.IsNullOrWhiteSpace(net))
        {
            var hit = links.FirstOrDefault(l => l.PlatformCode.Equals(net, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit.Url;
        }
        return links.FirstOrDefault(l => !l.PlatformCode.Equals(ShopSocialCatalog.Website, StringComparison.OrdinalIgnoreCase))?.Url;
    }

    private static void AddLegacy(List<ShopSocialLink> links, Shop shop, string code, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;
        if (ShopSocialNormalizer.TryNormalize(code, raw, out var url, out var handle, out _))
            AddBuilt(links, shop.ShopId, code, url, handle);
        else if (Uri.TryCreate(raw, UriKind.Absolute, out _))
            AddBuilt(links, shop.ShopId, code, raw, null);
    }

    private static void AddBuilt(List<ShopSocialLink> links, long shopId, string code, string url, string? handle)
    {
        var platform = ShopSocialCatalog.Find(code)!;
        links.Add(new ShopSocialLink
        {
            PlatformCode = platform.Code,
            Label = platform.Label,
            IconClass = platform.IconClass,
            Url = url,
            Handle = handle,
            TrackHref = ShopSocialCatalog.TrackHref(shopId, platform.Code),
            SortOrder = platform.DisplayOrder,
            Kind = platform.Kind
        });
    }
}
