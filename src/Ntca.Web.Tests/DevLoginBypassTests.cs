using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class DevLoginBypassTests
{
    [Fact]
    public void Production_rejects_bypass_even_when_flag_is_on()
    {
        var env = new StubHost("Production");
        var opt = new AuthOptions { DevBypassCode = true };
        Assert.False(DevLoginBypass.IsEnabled(env, opt));
        Assert.False(DevLoginBypass.Accepts(env, opt, DevLoginBypass.Code));
        Assert.False(DevLoginBypass.Accepts(env, opt, "888888"));
    }

    [Fact]
    public void Staging_rejects_bypass_even_when_flag_is_on()
    {
        var env = new StubHost("Staging");
        var opt = new AuthOptions { DevBypassCode = true };
        Assert.False(DevLoginBypass.IsEnabled(env, opt));
        Assert.False(DevLoginBypass.Accepts(env, opt, "888888"));
    }

    [Fact]
    public void Development_accepts_888888_only_when_flag_is_on()
    {
        var env = new StubHost("Development");
        Assert.True(DevLoginBypass.Accepts(env, new AuthOptions { DevBypassCode = true }, "888888"));
        Assert.True(DevLoginBypass.Accepts(env, new AuthOptions { DevBypassCode = true }, " 888888 "));
        Assert.False(DevLoginBypass.Accepts(env, new AuthOptions { DevBypassCode = true }, "123456"));
        Assert.False(DevLoginBypass.Accepts(env, new AuthOptions { DevBypassCode = false }, "888888"));
        Assert.Equal(6, DevLoginBypass.Code.Length);
        Assert.Equal("888888", DevLoginBypass.Code);
    }

    [Fact]
    public void Appsettings_json_does_not_enable_bypass()
    {
        var json = File.ReadAllText(FindWebFile("appsettings.json"));
        Assert.DoesNotContain("DevBypassCode", json);
        Assert.DoesNotContain("\"Auth\"", json);
        Assert.Contains("\"ServerId\": 0", json);
        Assert.Contains("\"ApiKey\": \"\"", json);
        Assert.Contains("reports@wte.net", json);
    }

    [Fact]
    public void Development_appsettings_enables_bypass()
    {
        var json = File.ReadAllText(FindWebFile("appsettings.Development.json"));
        Assert.Contains("\"DevBypassCode\": true", json);
    }

    [Fact]
    public void Startup_warns_when_bypass_is_active()
    {
        var program = File.ReadAllText(FindWebFile("Program.cs"));
        Assert.Contains("DevLoginBypass.IsEnabled", program);
        Assert.Contains("DEV login bypass code is active", program);
        Assert.Contains("Configure<AuthOptions>", program);
    }

    [Fact]
    public void Sadmin_login_still_requires_staff_and_surfaces_mail_hint()
    {
        var controller = File.ReadAllText(FindWebFile("Areas/Sadmin/Controllers/AccountController.cs"));
        Assert.Contains("u.IsStaff", controller);
        Assert.Contains("SUPERADMIN", controller);
        Assert.Contains("ViewBag.MailHint", controller);
        Assert.Contains("send.Delivered", controller);

        var codes = File.ReadAllText(FindWebFile("Services/AppServices.cs"));
        Assert.Contains("DEV_BYPASS_LOGIN", codes);
        Assert.Contains("WriteBypassAuditAsync", codes);

        var view = File.ReadAllText(FindWebFile("Areas/Sadmin/Views/Account/Login.cshtml"));
        Assert.Contains("ViewBag.MailHint", view);
        Assert.Contains("888888", view);
        Assert.Contains("egarrison@wte.net", view);
    }

    [Fact]
    public async Task Unconfigured_socketlabs_writes_dev_mail_and_does_not_claim_sent()
    {
        var root = Path.Combine(Path.GetTempPath(), "ntca-dev-mail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var env = new StubWebEnv(root) { EnvironmentName = "Development" };
            var sender = new CompositeEmailSender(
                Options.Create(new SocketLabsOptions { ServerId = 0, ApiKey = "" }),
                Options.Create(new EmailOptions { FromAddress = "reports@wte.net", FromName = "NTCA" }),
                env,
                NullLogger<CompositeEmailSender>.Instance,
                new ThrowingSmtp());

            Assert.False(sender.IsConfigured);
            var result = await sender.SendAsync(
                "egarrison@wte.net",
                "Your NTCA staff sign-in code",
                "Your National Trading Card Association sign-in code is 123456. It expires in 15 minutes.");

            Assert.False(result.Delivered);
            Assert.Equal("dev-file", result.Channel);
            Assert.Contains("Email is not configured", result.UserMessage);
            Assert.Contains("dev-mail", result.UserMessage);
            Assert.False(string.IsNullOrWhiteSpace(result.PickupPath));
            Assert.True(File.Exists(result.PickupPath!));
            Assert.Contains(Path.Combine("App_Data", "dev-mail"), result.PickupPath!);
            var body = await File.ReadAllTextAsync(result.PickupPath!);
            Assert.Contains("123456", body);
            Assert.Contains("egarrison@wte.net", body);
            Assert.Contains("reports@wte.net", body);
            Assert.Contains("Your NTCA staff sign-in code", body);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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

    private sealed class StubHost : IHostEnvironment
    {
        public StubHost(string name) => EnvironmentName = name;
        public string ApplicationName { get; set; } = "Ntca.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = ".";
        public string EnvironmentName { get; set; }
    }

    private sealed class StubWebEnv : IWebHostEnvironment
    {
        public StubWebEnv(string contentRoot) => ContentRootPath = contentRoot;
        public string ApplicationName { get; set; } = "Ntca.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ThrowingSmtp : ISmtpTransport
    {
        public Task SendAsync(EmailOptions email, string toEmail, string subject, string textBody, string? htmlBody, CancellationToken ct = default)
            => throw new InvalidOperationException("SMTP should not be called when Email:SmtpHost is empty.");
    }
}
