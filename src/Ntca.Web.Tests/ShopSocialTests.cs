using System.Text.Json.Nodes;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class ShopSocialTests
{
    [Theory]
    [InlineData("INSTAGRAM", "@boxseatcards", "https://www.instagram.com/boxseatcards/")]
    [InlineData("FACEBOOK", "burbanksportscards", "https://www.facebook.com/burbanksportscards")]
    [InlineData("TIKTOK", "@shopname", "https://www.tiktok.com/@shopname")]
    [InlineData("YOUTUBE", "HobbyShop", "https://www.youtube.com/@HobbyShop")]
    [InlineData("WHATNOT", "boxseat", "https://www.whatnot.com/user/boxseat")]
    [InlineData("X", "@ntca", "https://x.com/ntca")]
    [InlineData("THREADS", "ntca", "https://www.threads.net/@ntca")]
    [InlineData("BLUESKY", "shop.bsky.social", "https://bsky.app/profile/shop.bsky.social")]
    [InlineData("DISCORD", "hobby", "https://discord.gg/hobby")]
    [InlineData("EBAY", "myshop", "https://www.ebay.com/str/myshop")]
    [InlineData("TWITCH", "breaknight", "https://www.twitch.tv/breaknight")]
    [InlineData("REDDIT", "sportscards", "https://www.reddit.com/user/sportscards")]
    [InlineData("LINKEDIN", "ntca", "https://www.linkedin.com/company/ntca")]
    [InlineData("PINTEREST", "hobby", "https://www.pinterest.com/hobby/")]
    [InlineData("SNAPCHAT", "shop", "https://www.snapchat.com/add/shop")]
    [InlineData("LINKTREE", "findyourtrove", "https://linktr.ee/findyourtrove")]
    public void Handle_normalizes_to_canonical_https(string code, string raw, string expected)
    {
        Assert.True(ShopSocialNormalizer.TryNormalize(code, raw, out var url, out _, out var error), error);
        Assert.Equal(expected, url);
    }

    [Fact]
    public void Full_url_on_the_right_host_is_accepted()
    {
        Assert.True(ShopSocialNormalizer.TryNormalize("INSTAGRAM", "https://instagram.com/boxseatcards", out var url, out var handle, out var error), error);
        Assert.Equal("https://www.instagram.com/boxseatcards/", url);
        Assert.Equal("boxseatcards", handle);
    }

    [Fact]
    public void Website_gains_https_and_keeps_the_path()
    {
        Assert.True(ShopSocialNormalizer.TryNormalize("WEBSITE", "www.dacardworld.com/retail-stores", out var url, out _, out var error), error);
        Assert.Equal("https://www.dacardworld.com/retail-stores", url);
    }

    [Fact]
    public void Wrong_domain_is_rejected()
    {
        Assert.False(ShopSocialNormalizer.TryNormalize("INSTAGRAM", "https://www.facebook.com/boxseatcards", out _, out _, out var error));
        Assert.Contains("instagram.com", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Email_and_phone_normalize_but_are_not_sameAs()
    {
        Assert.True(ShopSocialNormalizer.TryNormalize("EMAIL", "shop@example.com", out var mail, out _, out _));
        Assert.Equal("mailto:shop@example.com", mail);
        Assert.True(ShopSocialNormalizer.TryNormalize("PHONE", "(818) 843-2600", out var tel, out var digits, out _));
        Assert.Equal("tel:8188432600", tel);
        Assert.Equal("8188432600", digits);
        Assert.False(ShopSocialCatalog.IsSameAs("EMAIL", mail));
        Assert.False(ShopSocialCatalog.IsSameAs("PHONE", tel));
    }

    [Fact]
    public void Tracking_uses_website_kind_and_social_net()
    {
        Assert.Equal("/t/website?shop=12", ShopSocialCatalog.TrackHref(12, "WEBSITE"));
        Assert.Equal("/t/social?shop=12&net=WHATNOT", ShopSocialCatalog.TrackHref(12, "WHATNOT"));
    }

    [Fact]
    public void Destination_reads_social_rows_then_legacy_columns()
    {
        var shop = new Shop
        {
            ShopId = 9,
            WebsiteUrl = "https://legacy.example/",
            FacebookUrl = "https://www.facebook.com/legacy",
            Socials =
            [
                new ShopSocial { ShopId = 9, PlatformCode = "WHATNOT", Url = "https://www.whatnot.com/user/boxseat", SortOrder = 60 }
            ]
        };
        Assert.Equal("https://www.whatnot.com/user/boxseat", ShopSocialLinks.Destination(shop, "social", "WHATNOT"));
        Assert.Equal("https://legacy.example", ShopSocialLinks.Destination(new Shop { ShopId = 9, WebsiteUrl = "https://legacy.example/" }, "website", null));
    }

    [Fact]
    public void Json_ld_sameAs_includes_social_https_and_skips_mailto()
    {
        var shop = new Shop
        {
            ShopId = 3,
            Name = "Boxseat Cards",
            Slug = "boxseat-cards",
            City = "Austin",
            StateCode = "TX",
            CountryCode = "US",
            UpdatedAt = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc),
            Socials =
            [
                new ShopSocial { PlatformCode = "WEBSITE", Url = "https://boxseatcards.com/", SortOrder = 10 },
                new ShopSocial { PlatformCode = "WHATNOT", Url = "https://www.whatnot.com/user/boxseat", SortOrder = 60 },
                new ShopSocial { PlatformCode = "EMAIL", Url = "mailto:shop@boxseatcards.com", SortOrder = 180 }
            ]
        };
        var json = ShopJsonLd.Build(new ShopSchemaInput
        {
            Shop = shop,
            Origin = "https://tradingcardsassociation.com",
            CanonicalUrl = "https://tradingcardsassociation.com/shops/tx/austin/boxseat-cards",
            AiSummary = ShopCopy.Summary(shop),
            SameAs = ShopSocialLinks.SameAs(shop)
        });
        Assert.Empty(ShopJsonLd.Validate(json));
        var store = JsonNode.Parse(json)!["@graph"]!.AsArray().OfType<JsonObject>()
            .First(o => o["@type"]?.GetValue<string>() is "HobbyShop" or "Store" or "SportingGoodsStore");
        var same = store["sameAs"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains("https://boxseatcards.com/", same);
        Assert.Contains("https://www.whatnot.com/user/boxseat", same);
        Assert.DoesNotContain(same, u => u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Markdown_lists_real_social_urls()
    {
        var shop = new Shop { Name = "Boxseat Cards", City = "Austin", StateCode = "TX", Slug = "boxseat-cards" };
        var links = new List<ShopSocialLink>
        {
            new() { PlatformCode = "WEBSITE", Label = "Website", Url = "https://boxseatcards.com/", TrackHref = "/t/website?shop=1" },
            new() { PlatformCode = "WHATNOT", Label = "Whatnot", Url = "https://www.whatnot.com/user/boxseat", TrackHref = "/t/social?shop=1&net=WHATNOT" }
        };
        var md = ShopMarkdown.Render(shop, "A shop.", socials: links);
        Assert.Contains("- Website: https://boxseatcards.com/", md);
        Assert.Contains("- Whatnot: https://www.whatnot.com/user/boxseat", md);
    }

    [Fact]
    public void Catalog_order_puts_whatnot_after_youtube_and_has_no_fa_icon()
    {
        var codes = ShopSocialCatalog.DisplayOrder.ToList();
        Assert.Equal(new[] { "WEBSITE", "FACEBOOK", "INSTAGRAM", "TIKTOK", "YOUTUBE", "WHATNOT" }, codes.Take(6));
        var whatnot = ShopSocialCatalog.Find("WHATNOT")!;
        Assert.True(string.IsNullOrEmpty(whatnot.IconClass));
        Assert.True(ShopSocialCatalog.IsCustomIcon("WHATNOT"));
    }

    [Fact]
    public void Directory_cards_take_the_first_four_links()
    {
        var shop = new Shop
        {
            ShopId = 1,
            Socials =
            [
                new ShopSocial { PlatformCode = "WEBSITE", Url = "https://a.example/", SortOrder = 10 },
                new ShopSocial { PlatformCode = "FACEBOOK", Url = "https://www.facebook.com/a", SortOrder = 20 },
                new ShopSocial { PlatformCode = "INSTAGRAM", Url = "https://www.instagram.com/a/", SortOrder = 30 },
                new ShopSocial { PlatformCode = "TIKTOK", Url = "https://www.tiktok.com/@a", SortOrder = 40 },
                new ShopSocial { PlatformCode = "WHATNOT", Url = "https://www.whatnot.com/user/a", SortOrder = 60 }
            ]
        };
        var top = ShopSocialLinks.Resolve(shop, 4);
        Assert.Equal(4, top.Count);
        Assert.Equal("WEBSITE", top[0].PlatformCode);
        Assert.Equal("TIKTOK", top[3].PlatformCode);
    }
}
