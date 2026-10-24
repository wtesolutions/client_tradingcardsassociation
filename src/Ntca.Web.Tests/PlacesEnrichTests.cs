using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class PlacesEnrichTests
{
    [Fact]
    public void Website_regex_finds_email_and_socials()
    {
        const string html = """
            <html><body>
              <a href="mailto:shop@hobbytyme.example">email</a>
              <a href="https://www.facebook.com/HobbyTyme">fb</a>
              <a href="https://www.instagram.com/hobbytyme/">ig</a>
              <a href="https://www.tiktok.com/@hobbytyme">tt</a>
              <a href="https://www.youtube.com/@hobbytyme">yt</a>
              <a href="https://x.com/hobbytyme">x</a>
              <a href="https://www.whatnot.com/user/hobbytyme">wn</a>
              <a href="https://www.ebay.com/str/hobbytyme">ebay</a>
              <a href="mailto:noreply@wixpress.com">ignore</a>
            </body></html>
            """;
        var parsed = WebsiteEnrich.Parse(html);
        Assert.Contains("shop@hobbytyme.example", parsed.Emails);
        Assert.DoesNotContain(parsed.Emails, e => e.Contains("wixpress", StringComparison.OrdinalIgnoreCase));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.Facebook));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.Instagram));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.TikTok));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.YouTube));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.X));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.Whatnot));
        Assert.True(parsed.Socials.ContainsKey(ShopSocialCatalog.Ebay));
    }

    [Fact]
    public void Ebay_non_store_links_are_ignored()
    {
        var parsed = WebsiteEnrich.Parse("""<a href="https://www.ebay.com/itm/123">item</a>""");
        Assert.False(parsed.Socials.ContainsKey(ShopSocialCatalog.Ebay));
    }

    [Fact]
    public async Task Website_fetch_uses_directory_bot_ua_and_falls_back_to_contact()
    {
        var seen = new List<HttpRequestMessage>();
        var handler = new ScriptedHandler(req =>
        {
            seen.Add(req);
            var path = req.RequestUri!.AbsolutePath;
            if (path is "/" or "")
                return Html("<html><body>home only</body></html>");
            if (path.Equals("/contact", StringComparison.OrdinalIgnoreCase))
                return Html("""<a href="https://www.facebook.com/HobbyTyme">fb</a><a href="mailto:info@hobby.example">e</a>""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hobby.example") };
        var enrich = new WebsiteEnrich(http, NullLogger<WebsiteEnrich>.Instance);
        var found = await enrich.FetchAsync("https://hobby.example", CancellationToken.None);
        Assert.Contains(seen, r => r.Headers.UserAgent.ToString().Contains("NTCA Directory Bot/1.0"));
        Assert.Contains("info@hobby.example", found.Emails);
        Assert.True(found.Socials.ContainsKey(ShopSocialCatalog.Facebook));
    }

    [Fact]
    public void Dry_run_cost_uses_enterprise_atmosphere_and_photo_skus()
    {
        var plan = new PlacesEnrichPlan(186, 0, 186, 186, 186, 186, 186, 186 * 5);
        var text = PlacesEnrich.FormatDryRun(plan);
        Assert.Contains("Text Search Enterprise", text);
        Assert.Contains("Place Details Enterprise + Atmosphere", text);
        Assert.Contains("Place Details Photos", text);
        Assert.Contains("needMatch=186", text);
        Assert.Contains("$35.00", text);
        Assert.Contains("$25.00", text);
        Assert.Contains("$7.00", text);
        var list = PlacesPricing.ListPrice(186, PlacesPricing.TextSearchEnterprisePerThousand)
                   + PlacesPricing.ListPrice(186, PlacesPricing.PlaceDetailsAtmospherePerThousand)
                   + PlacesPricing.ListPrice(930, PlacesPricing.PlacePhotoPerThousand);
        Assert.Equal(17.67m, Math.Round(list, 2));
        Assert.Equal(0m, PlacesPricing.AfterCap(186, PlacesPricing.TextSearchEnterpriseFree, PlacesPricing.TextSearchEnterprisePerThousand));
    }

    [Fact]
    public void Places_cli_parses_limit_and_max_calls()
    {
        var args = new[] { "places", "enrich", "--top", "100", "--limit", "30", "--max-calls", "20", "--rate-ms", "1500", "--dry-run" };
        Assert.Equal(100, PlacesCli.ParseInt(args, "--top"));
        Assert.Equal(30, PlacesCli.ParseInt(args, "--limit"));
        Assert.Equal(20, PlacesCli.ParseInt(args, "--max-calls"));
        Assert.Equal(1500, PlacesCli.ParseInt(args, "--rate-ms"));
    }

    [Fact]
    public void Visitor_controllers_do_not_take_places_enrich()
    {
        foreach (var type in new[]
                 {
                     typeof(Ntca.Web.Controllers.ShopsController),
                     typeof(Ntca.Web.Controllers.HomeController),
                     typeof(Ntca.Web.Controllers.MediaController)
                 })
        {
            var used = type.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType);
            Assert.DoesNotContain(typeof(PlacesEnrich), used);
            Assert.DoesNotContain(typeof(WebsiteEnrich), used);
        }
    }

    [Fact]
    public void Program_dispatches_places_as_local_cli()
    {
        var text = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "src", "Ntca.Web", "Program.cs"));
        Assert.Contains("\"places\"", text);
        Assert.Contains("PlacesCli.RunAsync", text);
        Assert.Contains("PlacesEnrich", text);
    }

    [Fact]
    public void Shop_detail_shows_google_reviews_with_attribution()
    {
        var cshtml = File.ReadAllText(TestPaths.WebFile("Views/Shops/Detail.cshtml"));
        Assert.Contains("data-testid=\"shop-google-reviews\"", cshtml);
        Assert.Contains("data-testid=\"shop-google-reviews-summary\"", cshtml);
        Assert.Contains("data-testid=\"shop-google-reviews-all\"", cshtml);
        Assert.Contains("See all reviews on Google", cshtml);
        Assert.Contains("data-review-clamp", cshtml);
        Assert.Contains("Read more", cshtml);
        Assert.Contains("gmaps-attr", cshtml);
        Assert.Contains("Google Maps", cshtml);
    }

    [Fact]
    public void Places_md_documents_key_and_skus()
    {
        var md = File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "docs", "PLACES.md"));
        Assert.Contains("GooglePlaces:ApiKey", md);
        Assert.Contains("dotnet user-secrets set \"GooglePlaces:ApiKey\" \"<KEY>\"", md);
        Assert.Contains("GooglePlaces__ApiKey", md);
        Assert.Contains("places enrich --top 100 --dry-run", md);
        Assert.Contains("Place Details Enterprise + Atmosphere", md);
        Assert.Contains("Text Search Enterprise", md);
        Assert.Contains("Caching compliance", md);
        Assert.Contains("place ID values indefinitely", md);
        Assert.Contains("14.3", md);
        Assert.Contains("TODO(places-cache-compliance)", File.ReadAllText(TestPaths.WebFile("Services/GooglePlacesSync.cs")));
    }

    private static HttpResponseMessage Html(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/html")
        };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}
