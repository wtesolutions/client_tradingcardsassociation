using Xunit;

namespace Ntca.Web.Tests;

public sealed class BioSeedTests
{
    [Theory]
    [InlineData("db/seed/SEED_40_about_bios.sql")]
    [InlineData("db/seed/SEED_18_legacy_content_fix.sql")]
    [InlineData("db/seed/SEED_19_bio_fix.sql")]
    public void Seed_files_do_not_glue_the_next_person_onto_craig_or_eric(string rel)
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root, rel));
        var craigStart = text.IndexOf("Craig Lloyd founded", StringComparison.Ordinal);
        Assert.True(craigStart >= 0, rel + " missing Craig bio");
        var ericStart = text.IndexOf("Eric Garrison started", StringComparison.Ordinal);
        Assert.True(ericStart >= 0, rel + " missing Eric bio");
        var jayStart = text.IndexOf("Jay Dudley started", StringComparison.Ordinal);

        var craigLen = ericStart > craigStart ? ericStart - craigStart : 1600;
        var craigBlock = text.Substring(craigStart, Math.Min(1600, craigLen));
        Assert.DoesNotContain("Eric Garrison started", craigBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jay Dudley started", craigBlock, StringComparison.Ordinal);

        var ericEnd = jayStart > ericStart ? jayStart : Math.Min(text.Length, ericStart + 1600);
        var ericBlock = text.Substring(ericStart, Math.Min(1600, ericEnd - ericStart));
        Assert.DoesNotContain("Jay Dudley started", ericBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Craig Lloyd founded", ericBlock, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "db", "seed", "SEED_40_about_bios.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("SEED_40");
    }
}
