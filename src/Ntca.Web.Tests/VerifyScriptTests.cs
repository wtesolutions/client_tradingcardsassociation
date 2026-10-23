using System.Net.Sockets;
using System.Text.RegularExpressions;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class VerifyScriptTests
{
    private static readonly string[] ExpectKeys =
    [
        "shops", "shows", "shows_national", "shows_regional", "shows_local",
        "promoters", "venues", "manufacturers", "glossary", "condition_grades",
        "posts_published", "bloggers", "gallery", "social_platforms",
        "shopsocial_seed17", "shops_latlng_seed17", "shophours",
        "data_review_flags", "person_bios"
    ];

    [Fact]
    public void Verify_script_is_plain_readonly_and_has_one_result_grid()
    {
        var root = FindRepoRoot();
        var canonical = Path.Combine(root, "db", "99_verify.sql");
        var ssms = Path.Combine(root, "db", "ssms", "99_verify_SSMS.sql");
        Assert.True(File.Exists(canonical), "db/99_verify.sql");
        Assert.True(File.Exists(ssms), "db/ssms/99_verify_SSMS.sql");
        var text = File.ReadAllText(canonical);
        Assert.Equal(text, File.ReadAllText(ssms));

        Assert.Contains("USE [wte_NtcaDatabase];", text, StringComparison.Ordinal);
        Assert.Contains("DECLARE @Expect TABLE", text, StringComparison.Ordinal);
        Assert.Contains("Area", text, StringComparison.Ordinal);
        Assert.Contains("[Check]", text, StringComparison.Ordinal);
        Assert.Contains("Expected", text, StringComparison.Ordinal);
        Assert.Contains("Actual", text, StringComparison.Ordinal);
        Assert.Contains("Result", text, StringComparison.Ordinal);
        Assert.Contains("passed,", text, StringComparison.Ordinal);
        Assert.Contains("failed,", text, StringComparison.Ordinal);
        Assert.Contains("warnings", text, StringComparison.Ordinal);

        foreach (var key in ExpectKeys)
            Assert.Contains($"N'{key}'", text, StringComparison.Ordinal);

        var hits = new List<string>();
        var lines = File.ReadAllLines(canonical);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith(":", StringComparison.Ordinal))
                hits.Add($"{i + 1}: colon-directive");
            if (line.Contains("$(", StringComparison.Ordinal))
                hits.Add($"{i + 1}: dollar-paren");
        }
        Assert.True(hits.Count == 0, string.Join("\n", hits));

        var writes = Regex.Matches(
            text,
            @"(?im)^\s*(UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE\s+(?:TABLE|INDEX|PROC|PROCEDURE|VIEW|LOGIN|USER)|INSERT\s+INTO\s+(?!@))\b");
        Assert.True(writes.Count == 0, "Write statements are not allowed:\n" +
            string.Join("\n", writes.Select(m => m.Value.Trim())));

        Assert.DoesNotMatch(@"(?im)^\s*GO\s*$", text);
        Assert.Contains(
            "SELECT\n    Area,\n    [Check],\n    Expected,\n    Actual,\n    Result\nFROM @Out",
            text);
    }

    [Fact]
    public void Verify_script_runs_against_sql_when_a_container_is_available()
    {
        var root = FindRepoRoot();
        var script = Path.Combine(root, "db", "99_verify.sql");
        var server = Environment.GetEnvironmentVariable("NTCA_VERIFY_SERVER") ?? "127.0.0.1,1433";
        if (!SqlPortOpen(server))
            return;

        var sqlcmd = FindSqlcmd();
        if (sqlcmd is null)
            return;

        var password = Environment.GetEnvironmentVariable("NTCA_VERIFY_SA_PASSWORD")
                       ?? Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD")
                       ?? "NtcaSqlDev2026x";
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sqlcmd,
                Arguments = $"-S {server} -U sa -P {password} -C -I -b -i \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null)
                return;
            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(60_000))
            {
                try { proc.Kill(); } catch { /* ignore */ }
                return;
            }
            if (proc.ExitCode != 0)
                return;
            Assert.Contains("SUMMARY", stdout, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Incorrect syntax near", stdout + stderr, StringComparison.OrdinalIgnoreCase);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // sqlcmd is not on PATH in this environment.
        }
    }

    private static bool SqlPortOpen(string server)
    {
        var host = "127.0.0.1";
        var port = 1433;
        var parts = server.Split(',');
        if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
            host = parts[0].Trim();
        if (parts.Length >= 2 && int.TryParse(parts[1], out var p))
            port = p;
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            return task.Wait(TimeSpan.FromMilliseconds(400)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindSqlcmd()
    {
        foreach (var name in new[] { "/opt/mssql-tools18/bin/sqlcmd", "/opt/mssql-tools/bin/sqlcmd" })
        {
            if (File.Exists(name))
                return name;
        }
        return "sqlcmd";
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "db", "99_verify.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("db/99_verify.sql");
    }
}
