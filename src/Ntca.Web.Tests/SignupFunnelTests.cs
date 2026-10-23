using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class SignupFunnelTests
{
    [Theory]
    [InlineData("collector", "who", "interests")]
    [InlineData("parent", "travel", "email")]
    [InlineData("shop_owner", "shop_find", "shop_tier")]
    [InlineData("promoter", "show", "email")]
    [InlineData("dealer", "interests", "email")]
    public void Next_follows_role_path(string role, string step, string next)
        => Assert.Equal(next, FunnelFlow.Next(role, step));

    [Fact]
    public void Back_from_who_is_null()
        => Assert.Null(FunnelFlow.Back(null, "who"));

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
                ["city"] = "Raleigh"
            })
        };
        var ask = FunnelFlow.ToAsk(funnel);
        Assert.Equal("collector", ask.HumanRole);
        Assert.Equal("27601", ask.Location?.Zip);
        Assert.Contains("pokemon", ask.Interests!);
        var sanitized = AgentPrivacy.Sanitize(default, ask);
        Assert.Equal("NC", sanitized.StateCode);
        Assert.Equal("276", sanitized.Zip3);
    }
}
