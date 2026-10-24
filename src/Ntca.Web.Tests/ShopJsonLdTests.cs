using System.Text.Json.Nodes;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class ShopJsonLdTests
{
    [Fact]
    public void Real_destination_shop_graph_parses_and_passes_schema_rules()
    {
        var input = DaveAndAdams();
        var json = ShopJsonLd.Build(input);
        var errors = ShopJsonLd.Validate(json);
        Assert.True(errors.Count == 0, string.Join("\n", errors) + "\n\n" + json);

        var root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("https://schema.org", root["@context"]!.GetValue<string>());
        var graph = root["@graph"]!.AsArray();
        var store = graph.OfType<JsonObject>().First(o => Types(o).Any(t => t is "HobbyShop" or "Store" or "SportingGoodsStore"));
        Assert.Contains("LocalBusiness", Types(store));
        Assert.Contains("Store", Types(store));
        Assert.Contains("HobbyShop", Types(store));
        Assert.Equal("trading card shop", store["additionalType"]!.GetValue<string>());
        Assert.EndsWith("#store", store["@id"]!.GetValue<string>());
        Assert.Equal(input.AiSummary, store["description"]!.GetValue<string>());
        Assert.Equal(input.CanonicalUrl, store["url"]!.GetValue<string>());
        Assert.Equal("https://www.dacardworld.com/retail-stores", store["sameAs"]![0]!.GetValue<string>());
        Assert.Null(store["aggregateRating"]);
        Assert.Null(store["openingHoursSpecification"]);
        Assert.Null(store["geo"]);
        Assert.Null(store["priceRange"]);
        Assert.Null(store["telephone"]);
        Assert.Equal("ViewAction", store["potentialAction"]!["@type"]!.GetValue<string>());
        Assert.EndsWith("#org", store["memberOf"]!["@id"]!.GetValue<string>());
        Assert.Contains("NTCA listing tier: Free Listing", store["memberOf"]!["description"]!.GetValue<string>());

        var crumbs = graph.OfType<JsonObject>().First(o => TypeOf(o) == "BreadcrumbList");
        var names = crumbs["itemListElement"]!.AsArray().OfType<JsonObject>().Select(i => i["name"]!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "Home", "Shops", "New York", "Williamsville", input.Shop.Name }, names);

        var page = graph.OfType<JsonObject>().First(o => TypeOf(o) == "WebPage");
        Assert.False(string.IsNullOrWhiteSpace(page["dateModified"]!.GetValue<string>()));
        var speakable = page["speakable"]!["cssSelector"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains(".shop-name", speakable);
        Assert.Contains(".shop-ai-summary", speakable);
    }

    [Fact]
    public void Burbank_sportscards_uses_sporting_goods_and_omits_empty_fields()
    {
        var shop = new Shop
        {
            ShopId = 1,
            Name = "Burbank Sportscards",
            Slug = "burbank-sportscards-burbank-ca",
            AddressLine1 = "935 N Hollywood Way",
            City = "Burbank",
            StateCode = "CA",
            PostalCode = "91505",
            CountryCode = "US",
            Phone = "818-843-2600",
            PublicEmail = "hobbytymesportscardsandtoys@gmail.com",
            UpdatedAt = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc)
        };
        var summary = ShopCopy.Summary(shop);
        var json = ShopJsonLd.Build(new ShopSchemaInput
        {
            Shop = shop,
            Origin = "https://tradingcardsassociation.com",
            CanonicalUrl = "https://tradingcardsassociation.com/shops/ca/burbank/burbank-sportscards-burbank-ca",
            AiSummary = summary,
            TierName = "Free Listing"
        });
        var errors = ShopJsonLd.Validate(json);
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        var store = JsonNode.Parse(json)!["@graph"]!.AsArray().OfType<JsonObject>()
            .First(o => Types(o).Any(t => t is "HobbyShop" or "Store" or "SportingGoodsStore"));
        Assert.Contains("LocalBusiness", Types(store));
        Assert.Contains("SportingGoodsStore", Types(store));
        Assert.Equal("818-843-2600", store["telephone"]!.GetValue<string>());
        Assert.Null(store["sameAs"]);
        Assert.Null(store["aggregateRating"]);
        Assert.Contains("Burbank, California", summary);
    }

    [Fact]
    public void Never_fabricates_aggregate_rating()
    {
        var input = DaveAndAdams();
        input = new ShopSchemaInput
        {
            Shop = input.Shop,
            Origin = input.Origin,
            CanonicalUrl = input.CanonicalUrl,
            AiSummary = input.AiSummary,
            TierName = input.TierName,
            ApprovedReviewCount = 0,
            AverageRating = 4.9m
        };
        var json = ShopJsonLd.Build(input);
        Assert.DoesNotContain("aggregateRating", json, StringComparison.Ordinal);
        Assert.Empty(ShopJsonLd.Validate(json));
    }

    [Fact]
    public void Hours_and_reviews_and_events_emit_only_when_real()
    {
        var input = DaveAndAdams();
        var shop = input.Shop;
        shop.Latitude = 42.9639m;
        shop.Longitude = -78.7378m;
        var hours = new List<ShopHours>
        {
            new() { DayOfWeek = 1, OpensAt = new TimeOnly(10, 0), ClosesAt = new TimeOnly(18, 0) },
            new() { DayOfWeek = 0, IsClosed = true }
        };
        var ev = new ShopEvent
        {
            Title = "In-store signing",
            Slug = "in-store-signing",
            StartsAt = new DateTime(2026, 11, 8),
            Status = "PUBLISHED",
            StateCode = "NY",
            City = "Williamsville",
            EventType = "SIGNING"
        };
        var json = ShopJsonLd.Build(new ShopSchemaInput
        {
            Shop = shop,
            Origin = input.Origin,
            CanonicalUrl = input.CanonicalUrl,
            AiSummary = ShopCopy.Summary(shop, hours),
            TierName = "Free Listing",
            Hours = hours,
            UpcomingEvents = [ev],
            ApprovedReviewCount = 3,
            AverageRating = 4.67m,
            Offers = [new ShopServiceOffer { Name = "Live card breaks" }]
        });
        var errors = ShopJsonLd.Validate(json);
        Assert.True(errors.Count == 0, string.Join("\n", errors) + "\n\n" + json);
        Assert.Contains("openingHoursSpecification", json);
        Assert.Contains("aggregateRating", json);
        Assert.Contains("In-store signing", json);
        Assert.Contains("makesOffer", json);
        Assert.Contains("\"latitude\":", json);
        Assert.DoesNotContain("Sunday", json);
    }

    [Fact]
    public void Invalid_json_is_rejected_by_validator()
    {
        var errors = ShopJsonLd.Validate("{not-json");
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Bots_are_filtered()
    {
        Assert.True(ImpressionTracker.IsBot("Mozilla/5.0 (compatible; Googlebot/2.1)"));
        Assert.True(ImpressionTracker.IsBot("GPTBot"));
        Assert.True(ImpressionTracker.IsBot(""));
        Assert.False(ImpressionTracker.IsBot("Mozilla/5.0 (Macintosh; Intel Mac OS X) Chrome/129.0.0.0"));
    }

    private static ShopSchemaInput DaveAndAdams()
    {
        var shop = new Shop
        {
            ShopId = 88,
            Name = "Dave & Adam's Card World (retail superstore)",
            Slug = "dave-adam-s-card-world-retail-superstore-williamsville-ny",
            City = "Williamsville",
            StateCode = "NY",
            CountryCode = "US",
            WebsiteUrl = "https://www.dacardworld.com/retail-stores",
            Description = "About 40,000 sq ft hobby superstore from one of the largest hobby distributors, with a huge sealed selection and in-store signings.",
            PrimarySpecialty = "TCG_MIXED",
            SourceSystem = "TOP100-2026-10-22",
            ImportNotes = "Destination shop; top-100 research 2026-10-22; rank 2",
            UpdatedAt = new DateTime(2026, 10, 22, 18, 0, 0, DateTimeKind.Utc)
        };
        return new ShopSchemaInput
        {
            Shop = shop,
            Origin = "https://tradingcardsassociation.com",
            CanonicalUrl = "https://tradingcardsassociation.com/shops/ny/williamsville/dave-adam-s-card-world-retail-superstore-williamsville-ny",
            AiSummary = ShopCopy.Summary(shop),
            TierName = "Free Listing"
        };
    }

    [Fact]
    public void Listing_json_ld_is_item_list_and_breadcrumb()
    {
        var json = ShopJsonLd.BuildListing(
            "https://tradingcardsassociation.com",
            "https://tradingcardsassociation.com/shops/az/phoenix",
            "Hobby shops in Phoenix, Arizona",
            "2 active listings.",
            [
                ("Boxseat Collectibles", "https://tradingcardsassociation.com/shops/az/phoenix/boxseat-collectibles-phoenix-az"),
                ("Hobby Tyme", "https://tradingcardsassociation.com/shops/nc/mebane/hobby-tyme-sports-cards-toys-mebane-nc")
            ],
            [
                (1, "https://tradingcardsassociation.com/", "Home"),
                (2, "https://tradingcardsassociation.com/shops", "Shops"),
                (3, "https://tradingcardsassociation.com/shops/az", "Arizona"),
                (4, "https://tradingcardsassociation.com/shops/az/phoenix", "Phoenix")
            ]);
        Assert.Empty(ShopJsonLd.ValidateListing(json));
        Assert.Contains("ItemList", json);
        Assert.Contains("BreadcrumbList", json);
        Assert.Contains("Boxseat Collectibles", json);
        Assert.DoesNotContain("aggregateRating", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Google_places_ratings_are_never_accepted_as_schema_input()
    {
        var input = DaveAndAdams();
        var json = ShopJsonLd.Build(input);
        Assert.DoesNotContain("aggregateRating", json, StringComparison.Ordinal);
        Assert.Contains("LocalBusiness", json);
    }

    private static IReadOnlyList<string> Types(JsonObject node)
    {
        var t = node["@type"];
        if (t is JsonValue v)
            return [v.GetValue<string>()];
        if (t is JsonArray a)
            return a.OfType<JsonValue>().Select(x => x.GetValue<string>()).ToList();
        return [];
    }

    private static string? TypeOf(JsonObject node) => Types(node).FirstOrDefault();
}
