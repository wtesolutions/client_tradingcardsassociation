using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class ManufacturerDetailTests
{
    [Fact]
    public void Empty_description_is_not_shown()
    {
        Assert.False(ManufacturerCopy.HasDescription(new Manufacturer()));
        Assert.False(ManufacturerCopy.HasDescription(new Manufacturer { DescriptionHtml = "   " }));
        Assert.True(ManufacturerCopy.HasDescription(new Manufacturer { DescriptionHtml = "<p>Licensed sets.</p>" }));
    }

    [Fact]
    public void Logo_src_is_omitted_when_unset()
    {
        Assert.Null(ManufacturerCopy.LogoSrc(new Manufacturer()));
        Assert.Equal("/media/manufacturers/skybox.jpg",
            ManufacturerCopy.LogoSrc(new Manufacturer { LogoUrl = "/media/manufacturers/skybox.jpg" }));
    }

    [Fact]
    public void Skybox_logo_file_is_a_real_jpeg()
    {
        var path = FindWebFile("wwwroot/uploads/manufacturers/skybox.jpg");
        var info = new FileInfo(path);
        Assert.True(info.Exists);
        Assert.True(info.Length > 1000);
        var header = new byte[3];
        using var fs = File.OpenRead(path);
        Assert.Equal(3, fs.Read(header, 0, 3));
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, header);
    }

    [Fact]
    public async Task Local_storage_serves_manufacturer_logo_key()
    {
        var webRoot = Path.GetDirectoryName(FindWebFile("wwwroot/uploads/manufacturers/skybox.jpg"))!;
        var contentRoot = Path.GetFullPath(Path.Combine(webRoot, "..", "..", ".."));
        var store = new LocalFileStorage(Options.Create(new StorageOptions
        {
            LocalRoot = "wwwroot/uploads"
        }), new StubEnv(contentRoot));
        Assert.Equal("/media/manufacturers/skybox.jpg", store.PublicUrl("manufacturers/skybox.jpg"));
        Assert.True(await store.ExistsAsync("manufacturers/skybox.jpg"));
        await using var stream = await store.OpenReadAsync("manufacturers/skybox.jpg");
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 1000);
    }

    [Fact]
    public void Migration_and_seed_are_plain_tsql_with_ssms_copies()
    {
        var root = FindRepoRoot();
        foreach (var rel in new[]
                 {
                     "db/07_manufacturer_detail.sql",
                     "db/ssms/07_manufacturer_detail_SSMS.sql",
                     "db/seed/SEED_61_skybox_logo.sql",
                     "db/ssms/SEED_61_skybox_logo_SSMS.sql"
                 })
            Assert.True(File.Exists(Path.Combine(root, rel)), rel);

        var migrate = File.ReadAllText(Path.Combine(root, "db", "07_manufacturer_detail.sql"));
        Assert.Equal(migrate, File.ReadAllText(Path.Combine(root, "db", "ssms", "07_manufacturer_detail_SSMS.sql")));
        Assert.Contains("DescriptionHtml", migrate);
        Assert.Contains("LogoUrl", migrate);
        Assert.DoesNotContain("$(", migrate);
        Assert.DoesNotContain(":setvar", migrate);

        var seed = File.ReadAllText(Path.Combine(root, "db", "seed", "SEED_61_skybox_logo.sql"));
        Assert.Equal(seed, File.ReadAllText(Path.Combine(root, "db", "ssms", "SEED_61_skybox_logo_SSMS.sql")));
        Assert.Contains("/media/manufacturers/skybox.jpg", seed);
        Assert.Contains("skybox", seed);
        Assert.DoesNotContain("DescriptionHtml", seed);
    }

    [Fact]
    public void Detail_view_renders_description_only_when_present()
    {
        var view = File.ReadAllText(FindWebFile("Views/Manufacturers/Detail.cshtml"));
        Assert.Contains("ManufacturerCopy.HasDescription", view);
        Assert.Contains("mfr-prose", view);
        Assert.DoesNotContain("placeholder", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mfr-logo", File.ReadAllText(FindWebFile("Views/Manufacturers/Index.cshtml")));
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

    private static string FindRepoRoot()
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

    private sealed class StubEnv : IWebHostEnvironment
    {
        public StubEnv(string contentRoot) => ContentRootPath = contentRoot;
        public string ApplicationName { get; set; } = "Ntca.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
