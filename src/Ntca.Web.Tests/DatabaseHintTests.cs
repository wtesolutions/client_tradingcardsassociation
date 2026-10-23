using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class DatabaseHintTests
{
    [Fact]
    public void Parse_reads_server_database_and_user_not_password()
    {
        var (server, database, user) = DatabaseHint.Parse(
            "Server=10.100.1.87,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=NtcaAppDev2026x;Encrypt=True");
        Assert.Equal("10.100.1.87,1433", server);
        Assert.Equal("wte_NtcaDatabase", database);
        Assert.Equal("ntca_app", user);
    }

    [Fact]
    public void Html_page_omits_the_password()
    {
        var html = DatabaseHint.HtmlPage(
            "Server=10.100.1.87,1433;Database=wte_NtcaDatabase;User ID=ntca_app;Password=NtcaAppDev2026x",
            new InvalidOperationException("A network-related or instance-specific error occurred."));
        Assert.Contains("10.100.1.87,1433", html);
        Assert.Contains("wte_NtcaDatabase", html);
        Assert.Contains("ntca_app", html);
        Assert.DoesNotContain("NtcaAppDev2026x", html);
        Assert.Contains("VPN", html);
        Assert.Contains("user-secrets", html);
        Assert.Contains("04_onboarding_funnel_SSMS.sql", html);
    }

    [Fact]
    public void IsUnreachable_matches_network_wording()
    {
        Assert.True(DatabaseHint.IsUnreachable(
            new InvalidOperationException("A network-related or instance-specific error occurred while establishing a connection to SQL Server.")));
        Assert.False(DatabaseHint.IsUnreachable(new InvalidOperationException("Null shop slug.")));
    }
}
