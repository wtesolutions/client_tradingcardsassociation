using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class SiteSeoTests
{
    [Fact]
    public void Layout_emits_canonical_open_graph_and_twitter()
    {
        var layout = File.ReadAllText(TestPaths.WebFile("Views/Shared/_Layout.cshtml"));
        foreach (var token in new[]
        {
            "rel=\"canonical\"", "og:type", "og:title", "og:description", "og:url",
            "og:image", "og:image:width", "og:image:height", "og:site_name",
            "twitter:card", "twitter:title", "twitter:description", "twitter:image",
            "application/ld+json"
        })
            Assert.Contains(token, layout);
    }

    [Fact]
    public void Shop_card_thumbnail_links_to_detail_with_name_alt()
    {
        var card = File.ReadAllText(TestPaths.WebFile("Views/Shops/_ShopCard.cshtml"));
        Assert.Contains("shop-card-media-link", card);
        Assert.Contains("data-testid=\"shop-thumb-link-@Model.Slug\"", card);
        Assert.Contains("data-testid=\"shop-name-@Model.Slug\"", card);
        Assert.Contains("alt=\"@Model.Name\"", card);
        Assert.Contains("href=\"@loc\"", card);
        Assert.Contains("aria-label=\"@Model.Name\"", card);
    }

    [Fact]
    public void Shop_detail_review_cards_and_attribution()
    {
        var cshtml = File.ReadAllText(TestPaths.WebFile("Views/Shops/Detail.cshtml"));
        Assert.Contains("data-testid=\"shop-google-review-author\"", cshtml);
        Assert.Contains("data-testid=\"shop-google-review-stars\"", cshtml);
        Assert.Contains("data-testid=\"shop-google-review-more\"", cshtml);
        Assert.Contains("See all reviews on Google", cshtml);
        Assert.Contains("g-review-avatar", cshtml);
        Assert.Contains("translate=\"no\"", cshtml);
    }

    [Fact]
    public void News_blog_posting_serializes()
    {
        var post = new Post
        {
            Title = "Halloween kickoff",
            Slug = "halloween-kickoff",
            Excerpt = "A note from the association.",
            AuthorName = "NTCA",
            PublishedAt = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc)
        };
        var json = NewsJsonLd.BuildPost(
            post,
            "https://tradingcardsassociation.com",
            "https://tradingcardsassociation.com/news/halloween-kickoff",
            "https://tradingcardsassociation.com/ntca-logo.png");
        Assert.Empty(NewsJsonLd.ValidatePost(json));
        Assert.Contains("BlogPosting", json);
        Assert.Contains("Halloween kickoff", json);
        Assert.DoesNotContain("aggregateRating", json, StringComparison.Ordinal);

        var index = NewsJsonLd.BuildIndex("https://tradingcardsassociation.com", [post]);
        Assert.Contains("ItemList", index);
        Assert.Contains("BreadcrumbList", index);
        Assert.True(System.Text.Json.Nodes.JsonNode.Parse(index) is not null);
    }

    [Fact]
    public void Site_meta_builds_absolute_urls()
    {
        Assert.Equal("National Trading Card Association", SiteMeta.SiteName);
        Assert.Contains("og:image:width", File.ReadAllText(TestPaths.WebFile("Views/Shared/_Layout.cshtml")));
    }

    [Fact]
    public void Testing_md_lists_thumb_and_review_testdids()
    {
        var md = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "TESTING.md"));
        Assert.Contains("shop-thumb-link-{slug}", md);
        Assert.Contains("shop-name-{slug}", md);
        Assert.Contains("shop-google-reviews-all", md);
        Assert.Contains("gmaps-attribution", md);
    }

    [Fact]
    public void Star_bar_and_initials()
    {
        Assert.Equal("★★★★☆", GooglePlacesUi.StarBar(4));
        Assert.Equal("★★★★★", GooglePlacesUi.StarBar(5));
        Assert.Equal("JD", GooglePlacesUi.Initials("Jane Doe"));
        Assert.Equal("A", GooglePlacesUi.Initials("Ada"));
        Assert.Equal("?", GooglePlacesUi.Initials(""));
    }
}
