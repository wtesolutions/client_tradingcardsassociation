using Xunit;

namespace Ntca.Web.Tests;

public sealed class PublicChromeTests
{
    [Fact]
    public void Public_layout_hides_preview_banner_and_staff_discovery_links()
    {
        var layout = File.ReadAllText(FindWebFile("Views/Shared/_Layout.cshtml"));
        Assert.DoesNotContain("Preview mode — pages are live for QA", layout);
        Assert.DoesNotContain("preview-bar", layout);
        Assert.Contains("Join the association →", layout);
        Assert.DoesNotContain("href=\"/robots.txt\"", layout);
        Assert.DoesNotContain("href=\"/llms.txt\"", layout);
        Assert.DoesNotContain("href=\"/sitemap.xml\"", layout);
        Assert.DoesNotContain("href=\"/SADMIN\">Staff", layout);
        Assert.Contains("href=\"/account\"", layout);
        Assert.Contains("href=\"/owner\"", layout);
    }

    [Fact]
    public void Discovery_routes_still_exist()
    {
        var src = File.ReadAllText(FindWebFile("Controllers/DiscoveryController.cs"));
        Assert.Contains("[HttpGet(\"/robots.txt\")]", src);
        Assert.Contains("[HttpGet(\"/llms.txt\")]", src);
        Assert.Contains("[HttpGet(\"/sitemap.xml\")]", src);
        Assert.Contains("X-Robots-Tag", File.ReadAllText(FindWebFile("Program.cs")));
    }

    [Fact]
    public void Shops_search_header_matches_directory_mock_copy()
    {
        var index = File.ReadAllText(FindWebFile("Views/Shops/Index.cshtml"));
        var bar = File.ReadAllText(FindWebFile("Views/Shops/_DirectoryBar.cshtml"));
        Assert.Contains("Find your next favorite card shop.", index);
        Assert.Contains("Shop name or specialty", bar);
        Assert.Contains("Search", bar);
        Assert.Contains("dir-loc-clear", bar);
        Assert.Contains("CategoryIcon", index);
        Assert.Contains("Distance (nearest)", index);
        Assert.Contains("Clear all", index);
    }

    private static string FindWebFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Ntca.Web", relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
