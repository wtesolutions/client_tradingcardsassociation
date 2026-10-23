using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class SmtpEmailTests
{
    [Fact]
    public void Smtp_configured_only_when_host_is_set()
    {
        Assert.False(new EmailOptions().IsSmtpConfigured);
        Assert.True(new EmailOptions { SmtpHost = "smtp.socketlabs.com" }.IsSmtpConfigured);
    }

    [Fact]
    public void Production_appsettings_does_not_contain_smtp_password()
    {
        var json = File.ReadAllText(FindWebFile("appsettings.json"));
        Assert.DoesNotContain("SmtpPassword", json);
        Assert.DoesNotContain("smtp.socketlabs.com", json);
        Assert.Contains("reports@wte.net", json);
    }

    [Fact]
    public void Development_and_staging_have_smtp_host_not_bypass_on_staging()
    {
        var dev = ParseObject(FindWebFile("appsettings.Development.json"));
        var staging = ParseObject(FindWebFile("appsettings.Staging.json"));
        AssertSmtpBlock(dev);
        AssertSmtpBlock(staging);

        Assert.True(dev.GetProperty("Auth").GetProperty("DevBypassCode").GetBoolean());
        Assert.False(staging.TryGetProperty("Auth", out _));
        Assert.DoesNotContain("DevBypassCode", File.ReadAllText(FindWebFile("appsettings.Staging.json")));
    }

    [Fact]
    public void Program_dispatches_email_cli_and_docs_name_staging_environment()
    {
        var program = File.ReadAllText(FindWebFile("Program.cs"));
        Assert.Contains("\"email\"", program);
        Assert.Contains("EmailCli.RunAsync", program);
        Assert.Contains("ISmtpTransport", program);

        var hosting = File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", "HOSTING.md"));
        Assert.Contains("Dev/test SMTP password committed by Eric's approval 2026-10-23", hosting);
        Assert.Contains("ASPNETCORE_ENVIRONMENT=Staging", hosting);
        Assert.Contains("email test --to", hosting);
        Assert.Contains("rotate in SocketLabs", hosting);

        var status = File.ReadAllText(Path.Combine(FindRepoRoot(), "PROJECT_STATUS.md"));
        Assert.Contains("Dev/test SMTP password committed by Eric's approval 2026-10-23", status);
    }

    [Fact]
    public void Email_cli_requires_to_address()
    {
        Assert.Equal(2, EmailCli.Help());
        Assert.Equal("you@example.com", EmailCli.ParseArg(["email", "test", "--to", "you@example.com"], "--to"));
        Assert.Null(EmailCli.ParseArg(["email", "test"], "--to"));
    }

    [Fact]
    public async Task Smtp_success_does_not_write_pickup()
    {
        var root = Path.Combine(Path.GetTempPath(), "ntca-smtp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var smtp = new FakeSmtp();
        try
        {
            var sender = NewSender(root, smtp, smtpHost: "smtp.socketlabs.com");
            Assert.True(sender.IsConfigured);
            var result = await sender.SendAsync("egarrison@wte.net", "NTCA email test", "hello 123456");
            Assert.True(result.Delivered);
            Assert.Equal("smtp", result.Channel);
            Assert.Equal(1, smtp.Sent);
            Assert.Equal("egarrison@wte.net", smtp.LastTo);
            Assert.False(Directory.Exists(Path.Combine(root, "App_Data", "dev-mail")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Smtp_failure_writes_dev_mail_and_does_not_claim_sent()
    {
        var root = Path.Combine(Path.GetTempPath(), "ntca-smtp-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sender = NewSender(root, new FakeSmtp { ThrowMessage = "authentication failed" }, smtpHost: "smtp.socketlabs.com");
            var result = await sender.SendAsync("egarrison@wte.net", "NTCA email test", "code 654321");
            Assert.False(result.Delivered);
            Assert.Equal("smtp-error", result.Channel);
            Assert.Contains("authentication failed", result.UserMessage);
            Assert.Contains("dev-mail", result.UserMessage);
            Assert.True(File.Exists(result.PickupPath));
            var body = await File.ReadAllTextAsync(result.PickupPath!);
            Assert.Contains("654321", body);
            Assert.Contains("reports@wte.net", body);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Smtp_host_wins_over_injection_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), "ntca-smtp-pref-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var smtp = new FakeSmtp();
        try
        {
            var sender = new CompositeEmailSender(
                Options.Create(new SocketLabsOptions { ServerId = 23854, ApiKey = "not-used" }),
                Options.Create(new EmailOptions
                {
                    FromAddress = "reports@wte.net",
                    SmtpHost = "smtp.socketlabs.com",
                    SmtpPort = 587,
                    SmtpUser = "server23854"
                }),
                new StubWebEnv(root),
                NullLogger<CompositeEmailSender>.Instance,
                smtp);
            var result = await sender.SendAsync("qa@example.com", "subj", "body");
            Assert.True(result.Delivered);
            Assert.Equal("smtp", result.Channel);
            Assert.Equal(1, smtp.Sent);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CompositeEmailSender NewSender(string root, ISmtpTransport smtp, string? smtpHost)
        => new(
            Options.Create(new SocketLabsOptions()),
            Options.Create(new EmailOptions
            {
                FromAddress = "reports@wte.net",
                FromName = "NTCA",
                SmtpHost = smtpHost ?? "",
                SmtpPort = 587,
                SmtpUser = "server23854"
            }),
            new StubWebEnv(root),
            NullLogger<CompositeEmailSender>.Instance,
            smtp);

    private static void AssertSmtpBlock(JsonElement root)
    {
        var email = root.GetProperty("Email");
        Assert.Equal("smtp.socketlabs.com", email.GetProperty("SmtpHost").GetString());
        Assert.Equal(587, email.GetProperty("SmtpPort").GetInt32());
        Assert.Equal("server23854", email.GetProperty("SmtpUser").GetString());
        Assert.Equal("reports@wte.net", email.GetProperty("FromAddress").GetString());
        Assert.False(string.IsNullOrWhiteSpace(email.GetProperty("SmtpPassword").GetString()));
    }

    private static JsonElement ParseObject(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        return doc.RootElement.Clone();
    }

    private static string FindWebFile(string relative)
    {
        var candidate = Path.Combine(FindRepoRoot(), "src", "Ntca.Web", relative);
        if (File.Exists(candidate))
            return candidate;
        throw new FileNotFoundException(relative);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Ntca.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    private sealed class FakeSmtp : ISmtpTransport
    {
        public int Sent { get; private set; }
        public string? LastTo { get; private set; }
        public string? ThrowMessage { get; set; }

        public Task SendAsync(EmailOptions email, string toEmail, string subject, string textBody, string? htmlBody, CancellationToken ct = default)
        {
            if (ThrowMessage is not null)
                throw new InvalidOperationException(ThrowMessage);
            Sent++;
            LastTo = toEmail;
            return Task.CompletedTask;
        }
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
}
