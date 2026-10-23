using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class ShopDirectoryTests
{
    [Fact]
    public void Haversine_burbank_to_nearby_point_is_about_a_few_miles()
    {
        var miles = ShopCopy.MilesBetween(34.1808, -118.3090, 34.2010, -118.3300);
        Assert.InRange(miles, 1, 5);
    }

    [Fact]
    public void Map_thumb_uses_openstreetmap_static_when_coords_exist()
    {
        var shop = new Shop { Latitude = 34.1808m, Longitude = -118.3090m };
        var url = ShopCopy.MapThumbUrl(shop);
        Assert.Contains("staticmap.openstreetmap.de", url);
        Assert.Contains("34.1808", url);
        Assert.NotNull(ShopCopy.MapTileUrl(shop));
        Assert.StartsWith("https://tile.openstreetmap.org/", ShopCopy.MapTileUrl(shop));
        Assert.Null(ShopCopy.MapThumbUrl(new Shop()));
    }

    [Fact]
    public void Origin_from_zip_uses_a_shop_with_matching_postal_and_coords()
    {
        var shops = new[]
        {
            new Shop { PostalCode = "91502", Latitude = 34.18m, Longitude = -118.31m },
            new Shop { PostalCode = "27601" }
        };
        var origin = ShopCopy.OriginFromZip(shops, "91502-1234");
        Assert.NotNull(origin);
        Assert.Equal(34.18, origin!.Value.Lat, 2);
        Assert.Null(ShopCopy.OriginFromZip(shops, "27601"));
        var fromCity = ShopCopy.OriginFromAsk(
            [new Shop { City = "Raleigh", Latitude = 35.78m, Longitude = -78.64m, PostalCode = null }],
            "27601",
            "Raleigh");
        Assert.NotNull(fromCity);
        Assert.Equal(35.78, fromCity!.Value.Lat, 2);
    }

    [Fact]
    public void State_name_is_the_full_word()
    {
        Assert.Equal("Missouri", ShopCopy.StateName("MO"));
        Assert.Equal("Missouri", ShopCopy.StateName("mo"));
        Assert.Equal("California", ShopCopy.StateName("CA"));
    }

    [Fact]
    public void Free_listing_badge_is_hidden_on_public_cards()
    {
        Assert.False(ShopCopy.ShowListingBadge("Free Listing"));
        Assert.False(ShopCopy.ShowListingBadge("FREE"));
        Assert.False(ShopCopy.ShowListingBadge(null));
        Assert.True(ShopCopy.ShowListingBadge("Featured Listing"));
        Assert.True(ShopCopy.ShowListingBadge("Premium Partner"));
    }

    [Fact]
    public void Shop_name_link_is_the_detail_url()
    {
        var shop = new Shop { Slug = "burbank-sportscards-burbank-ca", City = "Burbank", StateCode = "CA" };
        Assert.Equal("/shops/ca/burbank/burbank-sportscards-burbank-ca", ShopCopy.Loc(shop));
    }

    [Fact]
    public void State_hub_json_ld_uses_full_state_name()
    {
        var json = ShopJsonLd.BuildStateHub("https://tradingcardsassociation.com", "MO", 12);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var names = new List<string>();
        CollectNames(doc.RootElement, names);
        Assert.Contains("Missouri", names);
        Assert.Contains("Hobby shops in Missouri", names);
        Assert.DoesNotContain("MO", names);
        Assert.Contains("BreadcrumbList", json);
        Assert.Contains("/shops/mo", json);
    }

    private static void CollectNames(System.Text.Json.JsonElement el, List<string> names)
    {
        if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (el.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                names.Add(n.GetString()!);
            foreach (var p in el.EnumerateObject())
                CollectNames(p.Value, names);
        }
        else if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var i in el.EnumerateArray())
                CollectNames(i, names);
        }
    }

    [Fact]
    public void Specialty_and_phone_helpers()
    {
        Assert.Equal("Pokémon TCG", ShopCopy.SpecialtyLabel("POKEMON"));
        Assert.Equal("tel:+18188432600", ShopCopy.TelHref("+1 (818) 843-2600"));
        Assert.Equal("0 mi", ShopCopy.FormatMiles(0.02));
        Assert.Equal("2.4 mi", ShopCopy.FormatMiles(2.41));
        Assert.Equal("18 mi", ShopCopy.FormatMiles(18.2));
    }

    [Fact]
    public void Directory_pages_twelve()
    {
        Assert.Equal(12, ShopDirectory.PageSize);
        var shops = Enumerable.Range(1, 30).Select(i => new Shop { ShopId = i, Name = $"Shop {i:00}", City = "Raleigh", StateCode = "NC" }).ToList();
        var page1 = ShopDirectory.TakePage(shops, 1);
        var page3 = ShopDirectory.TakePage(shops, 3);
        Assert.Equal(12, page1.Count);
        Assert.Equal(6, page3.Count);
        Assert.Equal(3, ShopDirectory.PageCount(30));
    }

    [Fact]
    public void Specialty_filter_treats_all_as_every_category()
    {
        Assert.True(ShopDirectory.MatchesSpecialty("ALL", "POKEMON"));
        Assert.True(ShopDirectory.MatchesSpecialty("POKEMON", "POKEMON"));
        Assert.False(ShopDirectory.MatchesSpecialty("SPORTS", "POKEMON"));
        Assert.True(ShopDirectory.MatchesSpecialty("TCG_MIXED", "OTHER"));
        Assert.True(ShopDirectory.MatchesSpecialty("SPORTS", ""));
    }

    [Fact]
    public void Keyword_matches_name_and_description_not_unrelated_city()
    {
        var shop = new Shop { Name = "Coastal Cards", City = "Wilmington", StateCode = "NC", Description = "Pokémon singles and sealed", PrimarySpecialty = "POKEMON" };
        Assert.True(ShopDirectory.MatchesKeyword(shop, "coastal"));
        Assert.True(ShopDirectory.MatchesKeyword(shop, "pokémon"));
        Assert.False(ShopDirectory.MatchesKeyword(shop, "raleigh"));
    }

    [Fact]
    public void Owner_claimed_is_only_the_claim_status()
    {
        Assert.True(ShopCopy.IsClaimed(new Shop { ClaimStatus = "CLAIMED" }));
        Assert.False(ShopCopy.IsClaimed(new Shop { ClaimStatus = "UNCLAIMED" }));
        Assert.False(ShopCopy.IsClaimed(new Shop { ClaimStatus = "PENDING" }));
    }

    [Fact]
    public void Opening_status_is_hidden_without_hours_and_never_invents_closed()
    {
        Assert.Null(ShopCopy.OpeningStatus([], "NC"));
        Assert.Null(ShopCopy.OpeningStatus(null, "NC"));
        Assert.False(ShopCopy.IsOpenNow([], "NC"));
        var hours = new[]
        {
            new ShopHours { DayOfWeek = 1, OpensAt = new TimeOnly(10, 0), ClosesAt = new TimeOnly(18, 0) }
        };
        var mondayMorning = new DateTimeOffset(2026, 10, 26, 14, 0, 0, TimeSpan.Zero);
        var status = ShopCopy.OpeningStatus(hours, "NC", mondayMorning);
        Assert.NotNull(status);
        Assert.True(status!.IsOpen);
        Assert.Contains("Open until", status.Label);
    }

    [Fact]
    public void Directory_apply_filters_radius_and_sorts_by_name()
    {
        var shops = new[]
        {
            new Shop { ShopId = 1, Name = "Zebra", City = "Raleigh", StateCode = "NC", Latitude = 35.78m, Longitude = -78.64m, PrimarySpecialty = "SPORTS" },
            new Shop { ShopId = 2, Name = "Alpha", City = "Raleigh", StateCode = "NC", Latitude = 35.79m, Longitude = -78.64m, PrimarySpecialty = "POKEMON" },
            new Shop { ShopId = 3, Name = "Far Away", City = "Los Angeles", StateCode = "CA", Latitude = 34.05m, Longitude = -118.25m, PrimarySpecialty = "SPORTS" }
        };
        var q = ShopDirectory.Parse(null, "Raleigh, NC", null, "SPORTS", "name", null, "25", 35.78, -78.64, 1);
        var origin = ShopDirectory.ResolveOrigin(shops, q);
        var result = ShopDirectory.Apply(shops, q, origin?.Lat, origin?.Lng);
        Assert.Single(result);
        Assert.Equal("Zebra", result[0].Name);
    }

    [Fact]
    public void Active_filter_chips_include_default_radius_when_located()
    {
        var q = ShopDirectory.Parse("topps", "Raleigh, NC", null, "POKEMON", null, "1", null, 35.78, -78.64, 1);
        var chips = ShopDirectory.Chips(q, hasOrigin: true);
        Assert.Contains(chips, c => c.Key == "radiusMiles" && c.Label == "Within 25 miles");
        Assert.Contains(chips, c => c.Key == "specialty" && c.Label == "Pokémon");
        Assert.Contains(chips, c => c.Key == "openNow");
        Assert.Equal("radiusMiles", chips[0].Key);
    }

    [Fact]
    public void Removing_radius_chip_sets_any_distance()
    {
        var q = ShopDirectory.Parse(null, "Raleigh", null, "POKEMON", "distance", null, null, 35.78, -78.64, 1);
        var href = ShopDirectory.QueryString(q, drop: "radiusMiles");
        Assert.Contains("radiusMiles=any", href);
        Assert.Contains("specialty=POKEMON", href);
        Assert.Contains("location=Raleigh", href);
    }

    [Fact]
    public void Location_label_uses_the_city_part()
    {
        var q = ShopDirectory.Parse(null, "Raleigh, NC", null, null, null, null, null, 35.78, -78.64, 1);
        Assert.Equal("Raleigh", ShopDirectory.LocationLabel(q, true));
        Assert.Equal("27601", ShopDirectory.LocationLabel(
            ShopDirectory.Parse(null, "27601", null, null, null, null, null, null, null, 1), false));
    }

    [Fact]
    public void Category_tabs_have_icons_for_each_specialty()
    {
        foreach (var cat in ShopDirectory.Categories)
        {
            var svg = ShopDirectory.CategoryIcon(cat.Key);
            Assert.Contains("<svg", svg);
        }
    }
}
