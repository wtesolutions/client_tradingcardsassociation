using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class SignupFunnelTests
{
    [Theory]
    [InlineData("collector", "who", "zip")]
    [InlineData("collector", "zip", "interests")]
    [InlineData("parent", "travel", "done")]
    [InlineData("shop_owner", "shop_find", "shop_tier")]
    [InlineData("promoter", "show_find", "done")]
    [InlineData("dealer", "interests", "done")]
    public void Next_follows_role_path(string role, string step, string next)
        => Assert.Equal(next, FunnelFlow.Next(role, step));

    [Fact]
    public void Multi_role_runs_each_block_in_sequence_without_duplicate_shared_steps()
    {
        var path = FunnelFlow.OnboardingPath(["shop_owner", "collector", "promoter"]);
        Assert.Equal(
            new[] { "who", "zip", "interests", "experience", "travel", "shop_find", "shop_tier", "show_find", "done" },
            path);
        Assert.Equal("shop_find", FunnelFlow.Next(["collector", "shop_owner"], "travel"));
        Assert.Equal("zip", FunnelFlow.Next(["collector", "shop_owner"], "who"));
    }

    [Fact]
    public void Roles_are_multi_select_and_normalized()
    {
        var roles = FunnelFlow.NormalizeRoles(["Manufacturer", "collector", "owner", "collector"]);
        Assert.Equal(new[] { "collector", "shop_owner", "manufacturer" }, roles);
        Assert.Equal("[\"collector\",\"shop_owner\"]", FunnelFlow.WriteRoles(["shop_owner", "collector"]));
    }

    [Fact]
    public void Back_from_who_is_null()
        => Assert.Null(FunnelFlow.Back(Array.Empty<string>(), "who"));

    [Fact]
    public void Progress_on_who_is_not_full_before_role()
        => Assert.InRange(FunnelFlow.Progress(Array.Empty<string>(), "who"), 1, 20);

    [Theory]
    [InlineData("collector", "Collector")]
    [InlineData("shop_owner", "Local Card Shop")]
    [InlineData("dealer", "Dealer")]
    [InlineData("media", "Content Creator")]
    [InlineData("promoter", "Other")]
    [InlineData("manufacturer", "Manufacturer")]
    public void Hobby_segment_matches_signup_check(string role, string segment)
        => Assert.Equal(segment, FunnelFlow.HobbySegment(role));

    [Fact]
    public void ToAsk_reuses_concierge_fields()
    {
        var funnel = new Ntca.Core.SignupFunnel
        {
            HumanRole = "collector",
            RolesJson = FunnelFlow.WriteRoles(["collector"]),
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
    public void SplitName_keeps_a_single_field_simple()
    {
        Assert.Equal(("Eric", "Garrison"), FunnelFlow.SplitName("Eric Garrison"));
        Assert.Equal(("Ada", null), FunnelFlow.SplitName("Ada"));
    }

    [Fact]
    public void Funnel_report_splits_registration_from_onboarding()
    {
        var funnels = new[]
        {
            new Ntca.Core.SignupFunnel
            {
                Status = FunnelFlow.StatusRegistered,
                OnboardingStatus = FunnelFlow.OnboardNotStarted,
                HumanRole = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Ntca.Core.SignupFunnel
            {
                Status = FunnelFlow.StatusCompleted,
                OnboardingStatus = FunnelFlow.OnboardCompleted,
                HumanRole = "collector",
                RolesJson = FunnelFlow.WriteRoles(["collector", "shop_owner"]),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var report = FunnelReport.Build(funnels, [
            new Ntca.Core.SignupStepEvent { StepKey = "register", Action = "COMPLETE" },
            new Ntca.Core.SignupStepEvent { StepKey = "who", Action = "ENTER" }
        ]);
        Assert.Equal(2, report.Started);
        Assert.Equal(2, report.Registered);
        Assert.Equal(1, report.OnboardingCompleted);
        Assert.Contains(report.ByRole, r => r.Name == "collector" && r.Selected == 1 && r.Onboarded == 1);
        Assert.Contains(report.ByRole, r => r.Name == "shop_owner" && r.Selected == 1);
    }

    [Fact]
    public void Icon_covers_role_and_interest_cards()
    {
        foreach (var key in new[] { "collector", "parent", "shop_owner", "promoter", "dealer", "media", "manufacturer", "sports", "pokemon", "vintage", "graded" })
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
