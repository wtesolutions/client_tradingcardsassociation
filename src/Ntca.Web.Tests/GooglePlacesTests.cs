using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class GooglePlacesTests
{
    [Fact]
    public void High_name_and_city_confidence_auto_applies()
    {
        var shop = new Shop { Name = "Burbank Sportscards", City = "Burbank", StateCode = "CA" };
        var hit = new PlacesSearchHit("ChIJ123", "Burbank Sportscards", "935 N Hollywood Way, Burbank, CA 91505", 34.18m, -118.30m, null);
        var score = GooglePlacesMatchScore.Score(shop, hit);
        Assert.True(score >= GooglePlacesMatchScore.AutoApplyThreshold, score.ToString());
        Assert.True(GooglePlacesMatchScore.AutoApply(score, shop, hit));
    }

    [Fact]
    public void Weak_name_stays_in_review()
    {
        var shop = new Shop { Name = "Oak City Collectibles", City = "Raleigh", StateCode = "NC" };
        var hit = new PlacesSearchHit("ChIJ999", "Raleigh Coffee Roasters", "Downtown Raleigh, NC", null, null, null);
        var score = GooglePlacesMatchScore.Score(shop, hit);
        Assert.True(score < GooglePlacesMatchScore.AutoApplyThreshold, score.ToString());
        Assert.False(GooglePlacesMatchScore.AutoApply(score, shop, hit));
    }

    [Fact]
    public void Search_query_uses_real_address_parts()
    {
        var q = GooglePlacesMatchScore.SearchQuery(new Shop
        {
            Name = "Burbank Sportscards",
            AddressLine1 = "935 N Hollywood Way",
            City = "Burbank",
            StateCode = "CA",
            PostalCode = "91505"
        });
        Assert.Contains("Burbank Sportscards", q);
        Assert.Contains("935 N Hollywood Way", q);
        Assert.Contains("91505", q);
    }

    [Fact]
    public void Client_is_unconfigured_without_a_key()
    {
        var client = NewClient("", _ => new HttpResponseMessage(HttpStatusCode.OK));
        Assert.False(client.IsConfigured);
    }

    [Fact]
    public async Task Text_search_sends_places_api_new_field_mask()
    {
        HttpRequestMessage? seen = null;
        var client = NewClient("test-key", req =>
        {
            seen = req;
            return Json("""{"places":[{"id":"ChIJ1","displayName":{"text":"Burbank Sportscards"},"formattedAddress":"Burbank, CA","location":{"latitude":34.1,"longitude":-118.3}}]}""");
        });
        var hits = await client.SearchTextAsync("Burbank Sportscards Burbank CA", CancellationToken.None);
        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.EndsWith("/v1/places:searchText", seen.RequestUri!.AbsoluteUri);
        Assert.Equal(GooglePlacesClient.SearchFieldMask, seen.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Equal("test-key", seen.Headers.GetValues("X-Goog-Api-Key").Single());
        Assert.DoesNotContain("reviews", GooglePlacesClient.SearchFieldMask);
        Assert.Single(hits);
        Assert.Equal("ChIJ1", hits[0].PlaceId);
        Assert.Equal("Burbank Sportscards", hits[0].DisplayName);
    }

    [Fact]
    public void Phone_and_street_corroborate_auto_apply()
    {
        var shop = new Shop
        {
            Name = "Hobby Tyme Sports Cards",
            AddressLine1 = "198 S Fifth St",
            City = "Mebane",
            StateCode = "NC",
            Phone = "(919) 555-0100",
            Latitude = 36.095m,
            Longitude = -79.267m
        };
        var hit = new PlacesSearchHit(
            "ChIJ-ht",
            "Hobby Tyme Sports Cards & Toys",
            "198 S 5th St, Mebane, NC 27302",
            36.096m,
            -79.268m,
            null,
            "+1 919-555-0100");
        Assert.True(GooglePlacesMatchScore.PhoneMatch(shop.Phone, hit.NationalPhone));
        Assert.True(GooglePlacesMatchScore.StreetNumberMatch(shop, hit));
        Assert.True(GooglePlacesMatchScore.CloseDistance(shop, hit, 0.5));
        Assert.True(GooglePlacesMatchScore.AutoApply(GooglePlacesMatchScore.Score(shop, hit), shop, hit));
    }

    [Fact]
    public void Same_name_far_away_without_phone_stays_in_review()
    {
        var shop = new Shop
        {
            Name = "Ace Hobby",
            City = "Raleigh",
            StateCode = "NC",
            Latitude = 35.78m,
            Longitude = -78.64m
        };
        var hit = new PlacesSearchHit(
            "ChIJ-far",
            "Ace Hobby",
            "Phoenix, AZ 85001",
            33.45m,
            -112.07m,
            null);
        Assert.False(GooglePlacesMatchScore.AutoApply(GooglePlacesMatchScore.Score(shop, hit), shop, hit));
    }

    [Fact]
    public async Task Match_search_sends_location_bias_and_phone_mask()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var client = NewClient("test-key", req =>
        {
            seen = req;
            body = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"places":[{"id":"ChIJ1","displayName":{"text":"Hobby Tyme"},"formattedAddress":"Mebane, NC","nationalPhoneNumber":"(919) 555-0100","location":{"latitude":36.1,"longitude":-79.2}}]}""");
        });
        var hits = await client.SearchTextNearAsync("Hobby Tyme Mebane NC", 36.1m, -79.2m, 16093, CancellationToken.None);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.EndsWith("/v1/places:searchText", seen.RequestUri!.AbsoluteUri);
        Assert.Equal(GooglePlacesClient.MatchFieldMask, seen.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Contains("nationalPhoneNumber", GooglePlacesClient.MatchFieldMask);
        Assert.Contains("locationBias", body);
        Assert.Contains("pageSize", body);
        Assert.Single(hits);
        Assert.Equal("(919) 555-0100", hits[0].NationalPhone);
    }

    [Fact]
    public async Task Rich_details_mask_is_enterprise_atmosphere_and_parses_reviews()
    {
        HttpRequestMessage? seen = null;
        var client = NewClient("test-key", req =>
        {
            seen = req;
            return Json("""
                {
                  "id":"ChIJ1",
                  "displayName":{"text":"Hobby Tyme"},
                  "rating":4.8,
                  "userRatingCount":42,
                  "websiteUri":"https://hobbytyme.example",
                  "nationalPhoneNumber":"(919) 555-0100",
                  "businessStatus":"OPERATIONAL",
                  "editorialSummary":{"text":"Local card shop."},
                  "types":["store","point_of_interest"],
                  "primaryType":"store",
                  "reviews":[{
                    "rating":5,
                    "text":{"text":"Great singles wall."},
                    "relativePublishTimeDescription":"2 months ago",
                    "authorAttribution":{"displayName":"Pat","uri":"https://maps.google.com/pat"}
                  }]
                }
                """);
        });
        var details = await client.GetDetailsRichAsync("ChIJ1", CancellationToken.None);
        Assert.Equal(GooglePlacesClient.DetailsRichMask, seen!.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Contains("reviews", GooglePlacesClient.DetailsRichMask);
        Assert.Contains("editorialSummary", GooglePlacesClient.DetailsRichMask);
        Assert.Equal(4.8m, details!.Rating);
        Assert.Equal("https://hobbytyme.example", details.WebsiteUri);
        Assert.Equal("OPERATIONAL", details.BusinessStatus);
        Assert.Single(details.Reviews);
        Assert.Equal("Pat", details.Reviews[0].AuthorName);
        Assert.Equal("Great singles wall.", details.Reviews[0].Text);
    }

    [Fact]
    public async Task Details_mask_omits_hours_and_photos_when_owner_data_wins()
    {
        HttpRequestMessage? seen = null;
        var client = NewClient("test-key", req =>
        {
            seen = req;
            return Json("""{"id":"ChIJ1","rating":4.6,"userRatingCount":120,"googleMapsUri":"https://maps.google.com/?cid=1"}""");
        });
        var details = await client.GetDetailsAsync("ChIJ1", new PlacesDetailFields(false, false), CancellationToken.None);
        Assert.Equal(GooglePlacesClient.DetailsCoreMask, seen!.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.DoesNotContain("photos", seen.Headers.GetValues("X-Goog-FieldMask").Single());
        Assert.Equal(4.6m, details!.Rating);
        Assert.Equal(120, details.UserRatingCount);
    }

    [Fact]
    public void Weekday_lines_parse_google_hours_json()
    {
        var lines = GooglePlacesHours.WeekdayLines("""{"weekdayDescriptions":["Monday: 10 AM – 6 PM","Tuesday: Closed"]}""");
        Assert.Equal(2, lines.Count);
        Assert.Contains("Monday", lines[0]);
        Assert.Empty(GooglePlacesHours.WeekdayLines(null));
        Assert.Empty(GooglePlacesHours.WeekdayLines("{bad"));
    }

    [Fact]
    public void Nightly_batch_is_one_thirtieth()
    {
        var opt = Options.Create(new GooglePlacesOptions { ApiKey = "x" });
        Assert.Equal(7, Nightly(186, opt.Value));
        Assert.Equal(1, Nightly(10, opt.Value));
        Assert.Equal(4, Nightly(100, new GooglePlacesOptions { NightlyRefreshBatch = 4 }));
    }

    [Fact]
    public void Delay_until_three_utc_is_after_now()
    {
        var now = new DateTime(2026, 10, 23, 11, 0, 0, DateTimeKind.Utc);
        var delay = GooglePlacesHostedService.DelayUntilThreeUtc(now);
        Assert.Equal(TimeSpan.FromHours(16), delay);
        var afterThree = new DateTime(2026, 10, 23, 4, 0, 0, DateTimeKind.Utc);
        Assert.True(GooglePlacesHostedService.DelayUntilThreeUtc(afterThree).TotalHours > 20);
    }

    [Fact]
    public void First_match_plan_for_186_shops_is_one_text_search_each()
    {
        var plan = new GooglePlacesPlan(186, 0, 0, 186);
        Assert.Equal(186, plan.Total);
        var full = new GooglePlacesPlan(186, 186, 186 * 3, 186);
        Assert.Equal(930, full.Total);
    }

    [Fact]
    public void Visitor_controllers_do_not_take_the_google_http_client()
    {
        foreach (var type in new[]
                 {
                     typeof(Ntca.Web.Controllers.MediaController),
                     typeof(Ntca.Web.Controllers.ShopsController),
                     typeof(Ntca.Web.Controllers.HomeController)
                 })
        {
            var used = type.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType);
            Assert.DoesNotContain(typeof(IGooglePlacesClient), used);
        }
    }

    [Fact]
    public void Json_ld_still_omits_third_party_google_ratings()
    {
        var shop = new Shop
        {
            Name = "Test Cards",
            Slug = "test-cards-raleigh-nc",
            City = "Raleigh",
            StateCode = "NC",
            UpdatedAt = DateTime.UtcNow
        };
        var json = ShopJsonLd.Build(new ShopSchemaInput
        {
            Shop = shop,
            Origin = "https://tradingcardsassociation.com",
            CanonicalUrl = "https://tradingcardsassociation.com/shops/nc/raleigh/test-cards-raleigh-nc",
            AiSummary = ShopCopy.Summary(shop),
            ApprovedReviewCount = 0,
            AverageRating = 4.8m
        });
        Assert.DoesNotContain("aggregateRating", json);
    }

    private static int Nightly(int matched, GooglePlacesOptions opt)
    {
        if (opt.NightlyRefreshBatch > 0)
            return opt.NightlyRefreshBatch;
        return Math.Max(1, (int)Math.Ceiling(matched / 30.0));
    }

    private static GooglePlacesClient NewClient(string key, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var http = new HttpClient(new ScriptedHandler(respond));
        return new GooglePlacesClient(http, Options.Create(new GooglePlacesOptions { ApiKey = key }));
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }
}
