using Xunit;

namespace Ntca.Web.Tests;

public sealed class SqlCmdFreeTests
{
    [Fact]
    public void Every_sql_file_is_plain_tsql_without_sqlcmd_tokens()
    {
        var root = FindRepoRoot();
        var files = Directory.GetFiles(root, "*.sql", SearchOption.AllDirectories)
            .Where(p => !IsBuildOutput(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.NotEmpty(files);

        var hits = new List<string>();
        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith(':', StringComparison.Ordinal))
                    hits.Add($"{rel}:{i + 1}: line starts with ':'");
                if (line.Contains("$(", StringComparison.Ordinal))
                    hits.Add($"{rel}:{i + 1}: contains '$('");
            }
        }

        Assert.True(hits.Count == 0, "SQLCMD tokens are not allowed:\n" + string.Join("\n", hits));
    }

    private static bool IsBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase)
                           || p.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "db"))
                && File.Exists(Path.Combine(dir.FullName, "db", "00_create_database.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("db/00_create_database.sql");
    }
}
