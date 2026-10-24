using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class PlacesPriorityTests
{
    [Fact]
    public void Rank_is_featured_then_owner_then_pop_then_complete_then_nc()
    {
        var cities = new UsCitiesLookup();
        cities.Read("""
            city	state_id	population
            Raleigh	NC	1005457
            Mebane	NC	14117
            Phoenix	AZ	1650070
            """);
        var featured = Shop(1, "Hobby Tyme", "Mebane", "NC", featured: true);
        var owner = Shop(2, "Owner Shop", "Raleigh", "NC", claimed: true, website: true, phone: true);
        var bigCity = Shop(3, "Desert Cards", "Phoenix", "AZ", website: true);
        var completeNc = Shop(4, "Full NC", "Mebane", "NC", website: true, phone: true, address: true, coords: true);
        var thinNc = Shop(5, "Thin NC", "Mebane", "NC");
        var thinCa = Shop(6, "Thin CA", "Phoenix", "AZ");
        var ranked = PlacesPriority.Rank(
            [thinCa, thinNc, completeNc, bigCity, owner, featured],
            new HashSet<long>(),
            cities,
            _ => (false, true, false, false));
        // Featured, then owner, then higher metro pop (Phoenix 1.65M before Mebane),
        // then completeness, then NC. Phoenix incomplete still outranks Mebane complete.
        Assert.Equal(new long[] { 1, 2, 3, 6, 4, 5 }, ranked.Select(r => r.ShopId).ToArray());
        Assert.Equal(1, ranked[0].Rank);
        Assert.True(ranked[0].Parts.Featured);
        Assert.True(ranked[1].Parts.Owner);
        Assert.Equal(1650070, ranked[2].Parts.Population);
        Assert.Equal(1, ranked[2].Parts.Completeness);
        Assert.Equal(0, ranked[3].Parts.Completeness);
        Assert.Equal(4, ranked[4].Parts.Completeness);
        Assert.True(ranked[4].Parts.NorthCarolina);
        Assert.True(ranked[5].Parts.NorthCarolina);
    }

    [Fact]
    public void Owner_membership_counts_without_claim_status()
    {
        var shop = Shop(10, "Claimed via member", "Raleigh", "NC");
        var parts = PlacesPriorityParts.From(shop, hasVerifiedOwner: true, population: 1);
        Assert.True(parts.Owner);
        Assert.False(PlacesPriorityParts.From(shop, false, 1).Owner);
    }

    [Fact]
    public void Top_then_limit_skips_already_enriched()
    {
        var cities = new UsCitiesLookup();
        var shops = Enumerable.Range(1, 6).Select(i => Shop(i, "S" + i, "Raleigh", "NC", featured: i == 1)).ToList();
        var ranked = PlacesPriority.Rank(shops, new HashSet<long>(), cities, shop =>
        {
            var enriched = shop.ShopId is 1 or 2;
            return (enriched, !enriched, false, false);
        });
        var top = PlacesPriority.TakeTop(ranked, 5);
        Assert.Equal(5, top.Count);
        var batch = PlacesPriority.TakeWork(top, 2);
        Assert.Equal(new long[] { 3, 4 }, batch.Select(r => r.ShopId).ToArray());
        var next = PlacesPriority.TakeWork(top, 2);
        // same snapshot still starts at first needing work; resume is "enriched" flipping on the next real run
        var after = PlacesPriority.Rank(shops, new HashSet<long>(), cities, shop =>
        {
            var enriched = shop.ShopId is 1 or 2 or 3 or 4;
            return (enriched, !enriched, false, false);
        });
        var nextBatch = PlacesPriority.TakeWork(PlacesPriority.TakeTop(after, 5), 2);
        Assert.Equal(new long[] { 5 }, nextBatch.Select(r => r.ShopId).ToArray());
    }

    [Fact]
    public void Uscities_file_joins_nc_metro_population()
    {
        var path = UsCitiesLookup.ResolvePath(null);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        var cities = UsCitiesLookup.Load(path);
        Assert.True(cities.Count > 100);
        Assert.Equal(14117, cities.Population("Mebane", "NC"));
        Assert.Equal(1005457, cities.Population("Raleigh", "NC"));
        Assert.Equal(1438351, cities.Population("Charlotte", "nc"));
        Assert.Equal(0, cities.Population("Not A City", "NC"));
    }

    [Fact]
    public void Csv_export_has_score_parts()
    {
        var shop = Shop(7, "Hobby Tyme, Mebane", "Mebane", "NC", featured: true);
        var row = new PlacesRankedShop(
            1, shop, PlacesPriorityParts.From(shop, true, 14117),
            Enriched: false, NeedsMatch: true, NeedsDetails: false, NeedsWebsite: false);
        var dir = Path.Combine(Path.GetTempPath(), "ntca-places-" + Guid.NewGuid().ToString("n"));
        var file = Path.Combine(dir, "ranked.csv");
        PlacesPriority.WriteCsv(file, [row]);
        var csv = File.ReadAllText(file);
        Assert.Contains(PlacesRankedShop.CsvHeader(), csv);
        Assert.Contains("Hobby Tyme, Mebane", csv);
        Assert.Contains("14117", csv);
        Assert.Contains("featured=1", row.Line());
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Places_cli_parses_top_and_export()
    {
        var args = new[] { "places", "enrich", "--top", "100", "--limit", "30", "--export", "ranked.csv", "--dry-run" };
        Assert.Equal(100, PlacesCli.ParseInt(args, "--top"));
        Assert.Equal(30, PlacesCli.ParseInt(args, "--limit"));
        Assert.Equal("ranked.csv", PlacesCli.ParseString(args, "--export"));
    }

    [Fact]
    public void Places_md_documents_top_ranking_without_invented_weights()
    {
        var md = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "docs", "PLACES.md"));
        Assert.Contains("places enrich --top 100 --dry-run", md);
        Assert.Contains("--limit 30", md);
        Assert.Contains("IsEditorialFeatured", md);
        Assert.Contains("simplemaps", md.ToLowerInvariant());
        Assert.Contains("CC BY 4.0", md);
        Assert.DoesNotContain("weight =", md);
        var attr = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "data", "places", "README.md"));
        Assert.Contains("CC BY 4.0", attr);
        Assert.Contains("SimpleMaps", attr);
    }

    private static Shop Shop(
        long id, string name, string city, string state,
        bool featured = false, bool claimed = false,
        bool website = false, bool phone = false, bool address = false, bool coords = false)
        => new()
        {
            ShopId = id,
            Name = name,
            City = city,
            StateCode = state,
            IsEditorialFeatured = featured,
            ClaimStatus = claimed ? "CLAIMED" : "UNCLAIMED",
            WebsiteUrl = website ? "https://example.com" : null,
            Phone = phone ? "555-0100" : null,
            AddressLine1 = address ? "1 Main St" : null,
            Latitude = coords ? 35.8m : null,
            Longitude = coords ? -78.6m : null
        };
}
