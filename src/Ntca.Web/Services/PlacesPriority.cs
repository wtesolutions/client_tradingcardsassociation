using System.Text;
using Ntca.Core;

namespace Ntca.Web.Services;

public sealed record PlacesPriorityParts(
    bool Featured,
    bool Owner,
    int Population,
    int Completeness,
    bool NorthCarolina,
    long ShopId)
{
    public static PlacesPriorityParts From(Shop shop, bool hasVerifiedOwner, int population)
        => new(
            shop.IsEditorialFeatured,
            hasVerifiedOwner || ShopCopy.IsClaimed(shop),
            Math.Max(0, population),
            CompletenessOf(shop),
            string.Equals(shop.StateCode, "NC", StringComparison.OrdinalIgnoreCase),
            shop.ShopId);

    public static int CompletenessOf(Shop shop)
    {
        var n = 0;
        if (!string.IsNullOrWhiteSpace(shop.WebsiteUrl)) n++;
        if (!string.IsNullOrWhiteSpace(shop.Phone)) n++;
        if (!string.IsNullOrWhiteSpace(shop.AddressLine1)
            || (!string.IsNullOrWhiteSpace(shop.City) && !string.IsNullOrWhiteSpace(shop.PostalCode)))
            n++;
        if (shop.Latitude is not null && shop.Longitude is not null) n++;
        return n;
    }

    public int CompareTo(PlacesPriorityParts other)
    {
        var c = other.Featured.CompareTo(Featured);
        if (c != 0) return c;
        c = other.Owner.CompareTo(Owner);
        if (c != 0) return c;
        c = other.Population.CompareTo(Population);
        if (c != 0) return c;
        c = other.Completeness.CompareTo(Completeness);
        if (c != 0) return c;
        c = other.NorthCarolina.CompareTo(NorthCarolina);
        if (c != 0) return c;
        return ShopId.CompareTo(other.ShopId);
    }

    public string PartsLabel()
        => $"featured={(Featured ? 1 : 0)} owner={(Owner ? 1 : 0)} pop={Population} complete={Completeness}/4 nc={(NorthCarolina ? 1 : 0)}";
}

public sealed record PlacesRankedShop(
    int Rank,
    Shop Shop,
    PlacesPriorityParts Parts,
    bool Enriched,
    bool NeedsMatch,
    bool NeedsDetails,
    bool NeedsWebsite)
{
    public long ShopId => Shop.ShopId;
    public string Name => Shop.Name;
    public string CityState => $"{Shop.City}, {Shop.StateCode}";
    public bool NeedsWork => NeedsMatch || NeedsDetails || NeedsWebsite;

    public string Line()
        => $"rank={Rank} shopId={ShopId} name={Quote(Name)} city={Quote(CityState)} {Parts.PartsLabel()} enriched={(Enriched ? "yes" : "no")}";

    public static string CsvHeader()
        => "rank,shop_id,name,city,state,featured,owner,population,completeness,nc,enriched,needs_match,needs_details,needs_website";

    public string CsvLine()
        => string.Join(',',
            Rank,
            ShopId,
            Csv(Name),
            Csv(Shop.City),
            Csv(Shop.StateCode),
            Parts.Featured ? 1 : 0,
            Parts.Owner ? 1 : 0,
            Parts.Population,
            Parts.Completeness,
            Parts.NorthCarolina ? 1 : 0,
            Enriched ? "yes" : "no",
            NeedsMatch ? 1 : 0,
            NeedsDetails ? 1 : 0,
            NeedsWebsite ? 1 : 0);

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "'") + "\"";

    private static string Csv(string? value)
    {
        var raw = value ?? "";
        if (raw.Contains(',') || raw.Contains('"') || raw.Contains('\n'))
            return "\"" + raw.Replace("\"", "\"\"") + "\"";
        return raw;
    }
}

public static class PlacesPriority
{
    public static IReadOnlyList<PlacesRankedShop> Rank(
        IEnumerable<Shop> shops,
        IReadOnlySet<long> ownerShopIds,
        UsCitiesLookup cities,
        Func<Shop, (bool Enriched, bool NeedsMatch, bool NeedsDetails, bool NeedsWebsite)> status)
    {
        var rows = shops.Select(shop =>
        {
            var owner = ownerShopIds.Contains(shop.ShopId);
            var parts = PlacesPriorityParts.From(shop, owner, cities.Population(shop.City, shop.StateCode));
            var flag = status(shop);
            return new PlacesRankedShop(0, shop, parts, flag.Enriched, flag.NeedsMatch, flag.NeedsDetails, flag.NeedsWebsite);
        }).ToList();
        rows.Sort((a, b) => a.Parts.CompareTo(b.Parts));
        return rows.Select((row, i) => row with { Rank = i + 1 }).ToList();
    }

    public static IReadOnlyList<PlacesRankedShop> TakeTop(IReadOnlyList<PlacesRankedShop> ranked, int? top)
        => top is > 0 ? ranked.Take(top.Value).ToList() : ranked;

    public static IReadOnlyList<PlacesRankedShop> TakeWork(IReadOnlyList<PlacesRankedShop> ranked, int? limit)
    {
        var work = ranked.Where(r => r.NeedsWork);
        return limit is > 0 ? work.Take(limit.Value).ToList() : work.ToList();
    }

    public static string FormatList(IReadOnlyList<PlacesRankedShop> ranked)
    {
        if (ranked.Count == 0)
            return "ranked: (empty)";
        var sb = new StringBuilder();
        sb.AppendLine($"ranked {ranked.Count} shops (featured → owner → metro pop → completeness → NC):");
        foreach (var row in ranked)
            sb.AppendLine(row.Line());
        return sb.ToString().TrimEnd();
    }

    public static void WriteCsv(string path, IReadOnlyList<PlacesRankedShop> ranked)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var sb = new StringBuilder();
        sb.AppendLine(PlacesRankedShop.CsvHeader());
        foreach (var row in ranked)
            sb.AppendLine(row.CsvLine());
        File.WriteAllText(path, sb.ToString());
    }
}
