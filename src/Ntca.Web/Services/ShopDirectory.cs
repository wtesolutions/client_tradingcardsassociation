using System.Globalization;
using Ntca.Core;

namespace Ntca.Web.Services;

public sealed record DirectoryQuery
{
    public string Q { get; init; } = "";
    public string Location { get; init; } = "";
    public double? Lat { get; init; }
    public double? Lng { get; init; }
    public int? RadiusMiles { get; init; }
    public string Specialty { get; init; } = "";
    public bool OpenNow { get; init; }
    public string Sort { get; init; } = "";
    public int Page { get; init; } = 1;

    public bool HasKeyword => Q.Length > 0;
    public bool HasLocation => Location.Length > 0 || Lat is not null;
}

public sealed record DirectoryChip(string Key, string Value, string Label);

public static class ShopDirectory
{
    public const int PageSize = 12;
    public const int DefaultRadiusMiles = 25;
    public const int CandidateCap = 800;

    public static readonly (string Key, string Label)[] Categories =
    [
        ("", "All cards"),
        ("SPORTS", "Sports cards"),
        ("POKEMON", "Pokémon"),
        ("MAGIC", "Magic: The Gathering"),
        ("OTHER", "Other TCGs")
    ];

    public static readonly int[] RadiusChoices = [5, 10, 25, 50, 100];

    public static DirectoryQuery Parse(
        string? q,
        string? location,
        string? zip,
        string? specialty,
        string? sort,
        string? openNow,
        string? radiusMiles,
        double? lat,
        double? lng,
        int? page)
    {
        var loc = (location ?? "").Trim();
        if (loc.Length == 0)
            loc = (zip ?? "").Trim();
        var spec = NormalizeSpecialty(specialty);
        var radius = ParseRadius(radiusMiles);
        var pageNum = page is > 0 ? page.Value : 1;
        return new DirectoryQuery
        {
            Q = (q ?? "").Trim(),
            Location = loc,
            Lat = lat is >= -90 and <= 90 ? lat : null,
            Lng = lng is >= -180 and <= 180 ? lng : null,
            RadiusMiles = radius,
            Specialty = spec,
            OpenNow = IsTruthy(openNow),
            Sort = (sort ?? "").Trim().ToLowerInvariant(),
            Page = pageNum
        };
    }

    public static string NormalizeSpecialty(string? specialty)
    {
        var s = (specialty ?? "").Trim().ToUpperInvariant();
        return s switch
        {
            "SPORTS" or "SPORT" or "SPORTS_CARDS" => "SPORTS",
            "POKEMON" or "POKÉMON" => "POKEMON",
            "MAGIC" or "MTG" => "MAGIC",
            "OTHER" or "TCG" or "TCG_MIXED" or "NONSPORT" => "OTHER",
            _ => ""
        };
    }

    public static bool MatchesSpecialty(string? shopSpecialty, string filter)
    {
        if (string.IsNullOrEmpty(filter))
            return true;
        var spec = (shopSpecialty ?? "").Trim().ToUpperInvariant();
        if (spec is "ALL")
            return true;
        return filter switch
        {
            "SPORTS" => spec == "SPORTS",
            "POKEMON" => spec == "POKEMON",
            "MAGIC" => spec == "MAGIC",
            "OTHER" => spec is "TCG_MIXED" or "NONSPORT" or "OTHER",
            _ => true
        };
    }

    public static bool MatchesKeyword(Shop shop, string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return true;
        var term = q.Trim();
        if (shop.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
        if (shop.City.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
        if (shop.StateCode.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
        if (shop.PostalCode != null && shop.PostalCode.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            return true;
        if (shop.Description != null && shop.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;
        var label = ShopCopy.SpecialtyLabel(shop.PrimarySpecialty);
        return label.Contains(term, StringComparison.OrdinalIgnoreCase)
               || (shop.PrimarySpecialty != null && shop.PrimarySpecialty.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesLocationText(Shop shop, string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return true;
        var loc = location.Trim();
        var digits = new string(loc.Where(char.IsDigit).ToArray());
        if (digits.Length >= 3 && shop.PostalCode != null
            && shop.PostalCode.StartsWith(digits[..Math.Min(5, digits.Length)], StringComparison.OrdinalIgnoreCase))
            return true;
        var city = CityPart(loc);
        return city.Length > 0 && shop.City.Contains(city, StringComparison.OrdinalIgnoreCase);
    }

    public static string CityPart(string location)
    {
        var loc = location.Trim();
        if (loc.Length == 0 || loc.All(c => char.IsDigit(c) || c == '-'))
            return "";
        var comma = loc.IndexOf(',');
        return (comma > 0 ? loc[..comma] : loc).Trim();
    }

    public static int? ParseRadius(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim();
        if (t.Equals("any", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0
            ? n
            : null;
    }

    public static int EffectiveRadius(DirectoryQuery query, bool hasOrigin)
    {
        if (!hasOrigin)
            return 0;
        if (query.RadiusMiles is { } r)
            return r;
        return DefaultRadiusMiles;
    }

    public static bool IsDefaultRadius(DirectoryQuery query, bool hasOrigin)
        => hasOrigin && EffectiveRadius(query, true) == DefaultRadiusMiles && query.RadiusMiles is null;

    public static string ResolveSort(DirectoryQuery query, bool hasOrigin)
    {
        if (query.Sort is "name" or "distance" or "relevance")
            return query.Sort;
        if (query.HasKeyword)
            return "relevance";
        if (hasOrigin)
            return "distance";
        return "name";
    }

    public static IReadOnlyList<Shop> Apply(
        IEnumerable<Shop> shops,
        DirectoryQuery query,
        double? originLat,
        double? originLng,
        DateTimeOffset? now = null)
    {
        var hasOrigin = originLat is not null && originLng is not null;
        var radius = EffectiveRadius(query, hasOrigin);
        var filtered = shops.Where(s =>
        {
            if (!MatchesSpecialty(s.PrimarySpecialty, query.Specialty))
                return false;
            if (!MatchesKeyword(s, query.Q))
                return false;
            if (hasOrigin && radius > 0)
            {
                var miles = ShopCopy.MilesFrom(s, originLat, originLng);
                if (miles is null || miles > radius)
                    return false;
            }
            else if (!hasOrigin && query.Location.Length > 0 && !MatchesLocationText(s, query.Location))
            {
                return false;
            }
            if (query.OpenNow && !ShopCopy.IsOpenNow(s.Hours, s.StateCode, now))
                return false;
            return true;
        });

        var sort = ResolveSort(query, hasOrigin);
        IOrderedEnumerable<Shop> ordered = sort switch
        {
            "distance" => filtered
                .OrderBy(s => ShopCopy.MilesFrom(s, originLat, originLng) ?? double.MaxValue)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.ShopId),
            "relevance" => filtered
                .OrderBy(s => RelevanceRank(s, query.Q))
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.ShopId),
            _ => filtered
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.ShopId)
        };
        return ordered.ToList();
    }

    public static IReadOnlyList<Shop> TakePage(IReadOnlyList<Shop> shops, int page)
    {
        var p = page < 1 ? 1 : page;
        return shops.Skip((p - 1) * PageSize).Take(PageSize).ToList();
    }

    public static int PageCount(int total)
        => total <= 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);

    public static IReadOnlyList<DirectoryChip> Chips(DirectoryQuery query, bool hasOrigin)
    {
        var chips = new List<DirectoryChip>();
        if (query.Specialty.Length > 0)
        {
            var label = Categories.FirstOrDefault(c => c.Key == query.Specialty).Label;
            if (string.IsNullOrEmpty(label))
                label = query.Specialty;
            chips.Add(new DirectoryChip("specialty", query.Specialty, label));
        }
        var radius = EffectiveRadius(query, hasOrigin);
        if (hasOrigin && query.RadiusMiles is not null && radius != DefaultRadiusMiles)
        {
            var text = radius == 0 ? "Any distance" : $"Within {radius} miles";
            chips.Add(new DirectoryChip("radiusMiles", query.RadiusMiles.Value.ToString(CultureInfo.InvariantCulture), text));
        }
        if (query.OpenNow)
            chips.Add(new DirectoryChip("openNow", "1", "Open now"));
        return chips;
    }

    public static string QueryString(DirectoryQuery query, int? page = null, string? drop = null)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (drop != null && key.Equals(drop, StringComparison.OrdinalIgnoreCase))
                return;
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add(key + "=" + Uri.EscapeDataString(value));
        }
        Add("q", query.Q);
        Add("location", query.Location);
        Add("specialty", query.Specialty);
        if (query.RadiusMiles is { } r)
            Add("radiusMiles", r.ToString(CultureInfo.InvariantCulture));
        if (query.OpenNow)
            Add("openNow", "1");
        Add("sort", query.Sort);
        if (query.Lat is { } lat && drop != "lat")
            parts.Add("lat=" + lat.ToString("0.#####", CultureInfo.InvariantCulture));
        if (query.Lng is { } lng && drop != "lng")
            parts.Add("lng=" + lng.ToString("0.#####", CultureInfo.InvariantCulture));
        var p = page ?? query.Page;
        if (p > 1 && drop != "page")
            parts.Add("page=" + p.ToString(CultureInfo.InvariantCulture));
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    public static (double Lat, double Lng)? ResolveOrigin(IEnumerable<Shop> shops, DirectoryQuery query)
    {
        if (query.Lat is { } lat && query.Lng is { } lng)
            return (lat, lng);
        return ShopCopy.OriginFromAsk(shops, query.Location, CityPart(query.Location));
    }

    public static string LocationLabel(DirectoryQuery query, bool hasOrigin)
    {
        if (query.Location.Length > 0)
            return query.Location;
        if (query.Lat is not null && hasOrigin)
            return "your location";
        return "";
    }

    public static int RelevanceRank(Shop shop, string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return 2;
        if (shop.Name.StartsWith(q.Trim(), StringComparison.OrdinalIgnoreCase))
            return 0;
        if (shop.Name.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static bool IsTruthy(string? raw)
        => raw is "1" or "true" or "on" or "yes" or "TRUE" or "ON" or "YES";
}
