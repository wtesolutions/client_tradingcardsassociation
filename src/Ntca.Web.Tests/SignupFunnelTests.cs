using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class SignupFunnelTests
{
    [Theory]
    [InlineData("collector", "who", "zip")]
    [InlineData("collector", "zip", "interests")]
    [InlineData("parent", "travel", "email")]
    [InlineData("shop_owner", "shop_find", "shop_tier")]
    [InlineData("promoter", "show", "email")]
    [InlineData("dealer", "interests", "email")]
    public void Next_follows_role_path(string role, string step, string next)
        => Assert.Equal(next, FunnelFlow.Next(role, step));

    [Fact]
    public void Back_from_who_is_null()
        => Assert.Null(FunnelFlow.Back(null, "who"));

    [Fact]
    public void Progress_on_who_is_not_full_before_role()
        => Assert.InRange(FunnelFlow.Progress(null, "who"), 1, 20);

    [Theory]
    [InlineData("collector", "Collector")]
    [InlineData("shop_owner", "Local Card Shop")]
    [InlineData("dealer", "Dealer")]
    [InlineData("media", "Content Creator")]
    [InlineData("promoter", "Other")]
    public void Hobby_segment_matches_signup_check(string role, string segment)
        => Assert.Equal(segment, FunnelFlow.HobbySegment(role));

    [Fact]
    public void ToAsk_reuses_concierge_fields()
    {
        var funnel = new Ntca.Core.SignupFunnel
        {
            HumanRole = "collector",
            AnswersJson = FunnelFlow.WriteAnswers(new Dictionary<string, string>
            {
                ["interests"] = "pokemon,sports",
                ["zip"] = "27601",
                ["state"] = "NC",
                ["city"] = "Raleigh",
                ["travel"] = "30"
            })
        };
        var ask = FunnelFlow.ToAsk(funnel);
        Assert.Equal("collector", ask.HumanRole);
        Assert.Equal("27601", ask.Location?.Zip);
        Assert.Contains("pokemon", ask.Interests!);
        Assert.Equal(30, ask.TravelMiles);
        var sanitized = AgentPrivacy.Sanitize(default, ask);
        Assert.Equal(30, sanitized.TravelMiles);
        Assert.Equal("NC", sanitized.StateCode);
        Assert.Equal("276", sanitized.Zip3);
    }

    [Fact]
    public void Icon_covers_role_and_interest_cards()
    {
        foreach (var key in new[] { "collector", "parent", "shop_owner", "promoter", "dealer", "media", "sports", "pokemon", "vintage", "graded" })
            Assert.False(string.IsNullOrWhiteSpace(FunnelFlow.Icon(key)));
    }

    [Fact]
    public void Hit_reads_anonymous_name_and_url()
    {
        var row = new { Name = "Triangle Area Trading Cards", url = "/shops/nc/raleigh/x" };
        Assert.Equal("Triangle Area Trading Cards", FunnelFlow.Hit(row, "Name"));
        Assert.Equal("/shops/nc/raleigh/x", FunnelFlow.Hit(row, "url"));
    }
}
