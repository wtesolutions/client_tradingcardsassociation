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
}
