using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class LegacyContentTests
{
    [Theory]
    [InlineData("/about.html", "/about")]
    [InlineData("/gallery.html", "/gallery")]
    [InlineData("/directory.html", "/network")]
    [InlineData("/directory", "/network")]
    [InlineData("/shows.html", "/calendar")]
    [InlineData("/submit-event.html", "/shows/submit")]
    [InlineData("/join.html", "/signup")]
    [InlineData("/grading.html", "/grading")]
    [InlineData("/email-autoresponder.html", "/")]
    [InlineData("/retailers.html", "/shops")]
    [InlineData("members-area.html", "/account")]
    public void Legacy_paths_301_to_new_routes(string from, string to)
    {
        Assert.Equal(to, LegacyUrls.Resolve(from));
    }

    [Fact]
    public void Every_legacy_html_page_has_a_redirect()
    {
        var root = FindLegacyRoot();
        var files = Directory.GetFiles(root, "*.html").Select(Path.GetFileName).OrderBy(x => x).ToList();
        Assert.NotEmpty(files);
        var missing = files.Where(f => LegacyUrls.Resolve("/" + f) is null).ToList();
        Assert.True(missing.Count == 0, "Unmapped legacy HTML: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("assets/craig-lloyd.jpeg", "legacy/assets/craig-lloyd.jpeg")]
    [InlineData("legacy/assets/gallery-jordan-cutout.jpeg", "legacy/assets/gallery-jordan-cutout.jpeg")]
    [InlineData("google-cache/1/1.jpg", "google-cache/1/1.jpg")]
    public void Object_keys_normalize(string input, string expected)
        => Assert.Equal(expected, MediaPaths.ObjectKey(input));

    [Fact]
    public void Person_json_ld_is_a_person_with_breadcrumbs()
    {
        var person = new PersonBio
        {
            Slug = "craig-lloyd",
            FullName = "Craig Lloyd",
            RoleTitle = "Founder, National Trading Card Association",
            BioText = "First paragraph.\n\nSecond paragraph."
        };
        var json = PersonJsonLd.BuildPerson(person, "https://tradingcardsassociation.com", "https://tradingcardsassociation.com/about/craig-lloyd", "https://cdn.example/craig.jpg");
        Assert.Contains("\"@type\": \"Person\"", json);
        Assert.Contains("Craig Lloyd", json);
        Assert.Contains("BreadcrumbList", json);
        Assert.Contains("First paragraph.", json);
        Assert.DoesNotContain("Second paragraph.", json);
        Assert.Equal("First paragraph.", PersonJsonLd.FirstParagraph(person.BioText));
    }

    [Fact]
    public void Gallery_json_ld_emits_image_objects_and_categories()
    {
        var item = new GalleryItem
        {
            Slug = "gallery-jordan-cutout",
            Caption = "Show floor · Jordan cutout",
            AltText = "Life-size Michael Jordan Bulls cardboard cutout",
            LegacyPath = "assets/gallery-jordan-cutout.jpeg"
        };
        Assert.Equal("Shows", GalleryJsonLd.CategoryOf(item));
        var json = GalleryJsonLd.Build([(item, "https://cdn.example/jordan.jpg")], "https://tradingcardsassociation.com", "https://tradingcardsassociation.com/gallery");
        Assert.Contains("ImageGallery", json);
        Assert.Contains("ImageObject", json);
        Assert.Contains("gallery-jordan-cutout", json);
    }

    [Fact]
    public void Grading_catalog_copied_fifty_legacy_services()
    {
        Assert.Equal(50, GradingCatalog.All.Count);
        Assert.Contains(GradingCatalog.All, g => g.Name == "PSA");
        Assert.Contains(GradingCatalog.All, g => g.WebsiteUrl.Contains("psacard.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Local_file_store_reads_legacy_site_and_builds_media_url()
    {
        var legacy = FindLegacyRoot();
        var uploads = Path.Combine(Path.GetTempPath(), "ntca-uploads-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploads);
        var env = new StubEnv(Path.GetDirectoryName(legacy)!);
        var store = new LocalFileStorage(Options.Create(new StorageOptions
        {
            Provider = "Local",
            LocalRoot = uploads,
            LegacyRoot = legacy
        }), env);

        Assert.False(store.UsesObjectStorage);
        Assert.Equal("/media/legacy/assets/craig-lloyd.jpeg", store.PublicUrl("assets/craig-lloyd.jpeg"));
        Assert.True(await store.ExistsAsync("assets/craig-lloyd.jpeg"));
        await using var stream = await store.OpenReadAsync("assets/craig-lloyd.jpeg");
        Assert.NotNull(stream);
        Assert.True(stream!.Length > 0);
    }

    [Fact]
    public void Storage_options_select_s3_only_when_provider_and_bucket_set()
    {
        var off = new StorageOptions { Provider = "Local", Bucket = "ntca-tradingcardsassociation-media" };
        Assert.False(off.UseS3);
        var on = new StorageOptions { Provider = "S3", Bucket = "ntca-tradingcardsassociation-media" };
        Assert.True(on.UseS3);
        Assert.Equal("ntca-tradingcardsassociation-media", on.ResolvedBucket);
    }

    private static string FindLegacyRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "legacy-site");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "about.html")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("legacy-site/");
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
