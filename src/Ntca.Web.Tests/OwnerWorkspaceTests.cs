using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class OwnerWorkspaceTests
{
    [Theory]
    [InlineData("owner@hobbytyme.com", "https://hobbytyme.com", true)]
    [InlineData("owner@hobbytyme.com", "https://www.hobbytyme.com", true)]
    [InlineData("owner@gmail.com", "https://hobbytyme.com", false)]
    [InlineData("not-an-email", "https://hobbytyme.com", false)]
    public void Email_domain_match_is_conservative(string email, string website, bool expected)
        => Assert.Equal(expected, ShopAdminCopy.EmailDomainMatchesWebsite(email, website));

    [Fact]
    public void Editorial_featured_is_not_destination_copy()
    {
        var shop = new Shop
        {
            Name = "Hobby Tyme",
            IsEditorialFeatured = true,
            SourceSystem = "manual"
        };
        Assert.Equal("Featured", ShopAdminCopy.DesignationLabel(shop));
        Assert.Contains("not paid", ShopAdminCopy.DesignationReviewState(shop), StringComparison.OrdinalIgnoreCase);
        Assert.True(ShopCopy.IsFeatured(shop));
    }

    [Fact]
    public void Destination_suffix_is_never_a_paid_designation()
    {
        var shop = new Shop { SourceSystem = "TOP100-2026-10-22", ImportNotes = "Destination shop" };
        Assert.Equal(ShopAdminCopy.DestinationReviewLabel, ShopAdminCopy.DesignationLabel(shop));
        Assert.Contains("imported, review needed", ShopAdminCopy.SourceLabel(shop));
    }

    [Fact]
    public void Only_admin_roles_can_impersonate()
    {
        var admin = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "ADMIN")], "t"));
        var editor = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "EDITOR")], "t"));
        Assert.True(ImpersonationService.CanImpersonate(admin));
        Assert.False(ImpersonationService.CanImpersonate(editor));
    }

    [Fact]
    public void Funnel_zero_is_an_em_dash()
    {
        Assert.Equal("—", ShopAdminCopy.DashIfZero(0));
        Assert.Equal("Registration", ShopAdminCopy.FunnelStepLabel("register"));
        Assert.Equal("Find your shop", ShopAdminCopy.FunnelStepLabel("shop_find"));
    }

    [Fact]
    public void Hobby_tyme_seed_is_editorial_and_email_is_opt_in()
    {
        var sql = File.ReadAllText(TestPaths.RepoFile("db/seed/SEED_21_hobby_tyme_featured.sql"));
        Assert.Contains("hobby-tyme-sports-cards-toys-mebane-nc", sql);
        Assert.Contains("Featured by Eric 2026-10-23 (editorial)", sql);
        Assert.Contains("DECLARE @OwnerEmail nvarchar(320) = N''", sql);
        Assert.DoesNotContain("jay@", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Leaves Cards And Stuff", sql);
        Assert.DoesNotContain("PAID", sql);
        Assert.DoesNotContain("CurrentTierId", sql);
    }
}

public sealed class SadminShellTests
{
    [Fact]
    public void Sadmin_layout_keeps_existing_hrefs_and_drops_join()
    {
        var layout = File.ReadAllText(TestPaths.WebFile("Areas/Sadmin/Views/Shared/_SadminLayout.cshtml"));
        Assert.DoesNotContain("Join the association", layout);
        Assert.Contains("/SADMIN/dashboard", layout);
        Assert.Contains("/SADMIN/queue/events", layout);
        Assert.Contains("/SADMIN/queue/edits", layout);
        Assert.Contains("/SADMIN/queue/drafts", layout);
        Assert.Contains("/SADMIN/queue/claims", layout);
        Assert.Contains("/SADMIN/shops", layout);
        Assert.Contains("/SADMIN/manufacturers", layout);
        Assert.Contains("/SADMIN/queue/onboarding", layout);
        Assert.Contains("/SADMIN/funnel", layout);
        Assert.Contains("/SADMIN/agents", layout);
        Assert.Contains("/SADMIN/google", layout);
        Assert.Contains("/SADMIN/social", layout);
        Assert.Contains("data-testid=\"sadmin-nav\"", layout);
        Assert.Contains("data-testid=\"impersonate-banner\"", layout);
        Assert.Contains("data-testid=\"impersonate-stop\"", layout);
    }

    [Fact]
    public void Public_and_admin_testdids_from_testing_md_exist()
    {
        var files = new[]
        {
            "Views/Shared/_Layout.cshtml",
            "Views/Shops/_DirectoryBar.cshtml",
            "Views/Shops/Index.cshtml",
            "Views/Shops/Detail.cshtml",
            "Views/Shops/_ShopCard.cshtml",
            "Areas/Sadmin/Views/Shared/_SadminLayout.cshtml",
            "Areas/Sadmin/Views/Account/Login.cshtml",
            "Areas/Sadmin/Views/Dashboard/Index.cshtml",
            "Areas/Sadmin/Views/Google/Index.cshtml",
            "Areas/Sadmin/Views/Queue/Shops.cshtml",
            "Areas/Owner/Views/Shop/Index.cshtml",
            "Views/Signup/Index.cshtml",
            "Views/Gallery/Index.cshtml"
        };
        var blob = string.Join('\n', files.Select(TestPaths.WebFile).Select(File.ReadAllText));
        foreach (var id in new[]
        {
            "header-join", "shops-q", "shops-search-submit", "shops-result-count",
            "shop-featured-ribbon", "sadmin-login-email", "dashboard-h1",
            "impersonate-owner", "owner-logo-input", "owner-photo-input",
            "signup-form", "gallery-grid", "lightbox",
            "google-places-page", "google-review-approve", "shop-google-reviews"
        })
            Assert.Contains($"data-testid=\"{id}\"", blob);
    }

    [Fact]
    public void Testing_md_and_fixtures_are_present()
    {
        var root = TestPaths.RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "TESTING.md")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "fixtures", "logo.png")));
        for (var i = 1; i <= 6; i++)
            Assert.True(File.Exists(Path.Combine(root, "tests", "fixtures", $"photo-{i}.png")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "e2e", "playwright.config.ts")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "e2e", "smoke.spec.ts")));
        var github = Path.Combine(root, ".github");
        if (Directory.Exists(github))
        {
            foreach (var file in Directory.GetFiles(github, "*.yml", SearchOption.AllDirectories))
                Assert.DoesNotContain("tests/e2e", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }
}

internal static class TestPaths
{
    public static string WebFile(string relative)
    {
        var path = Path.Combine(RepoRoot(), "src", "Ntca.Web", relative);
        if (!File.Exists(path))
            throw new FileNotFoundException(relative);
        return path;
    }

    public static string RepoFile(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative);
        if (!File.Exists(path))
            throw new FileNotFoundException(relative);
        return path;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "db", "00_create_database.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }
}
