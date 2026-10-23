using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class StorageSyncTests
{
    [Theory]
    [InlineData("wwwroot/uploads/manufacturers/skybox.jpg", "manufacturers/skybox.jpg")]
    [InlineData("uploads/manufacturers/skybox.jpg", "manufacturers/skybox.jpg")]
    [InlineData("manufacturers/skybox.jpg", "manufacturers/skybox.jpg")]
    [InlineData("gplace/12/1.jpg", "gplace/12/1.jpg")]
    [InlineData("legacy-site/assets/craig-lloyd.jpeg", "legacy/assets/craig-lloyd.jpeg")]
    [InlineData("assets/graded-slabs.jpeg", "legacy/assets/graded-slabs.jpeg")]
    public void Object_keys_are_bucket_root_not_uploads_prefix(string input, string expected)
        => Assert.Equal(expected, MediaPaths.ObjectKey(input));

    [Fact]
    public void Catalog_includes_uploads_and_legacy_assets_with_s3_keys()
    {
        var root = FindRepoRoot();
        var web = Path.Combine(root, "src", "Ntca.Web");
        var items = LocalMediaCatalog.Enumerate(web, new StorageOptions
        {
            LocalRoot = "wwwroot/uploads",
            LegacyRoot = Path.Combine(root, "legacy-site")
        });
        var keys = items.Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("manufacturers/skybox.jpg", keys);
        Assert.Contains("legacy/assets/graded-slabs.jpeg", keys);
        Assert.Contains("legacy/assets/craig-lloyd.jpeg", keys);
        Assert.DoesNotContain(keys, k => k.Contains(".gitkeep", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(keys, k => k.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
        Assert.All(LocalMediaCatalog.ProbeKeys, key => Assert.Contains(key, keys));
        var skybox = items.First(i => i.Key == "manufacturers/skybox.jpg");
        Assert.Equal("image/jpeg", skybox.ContentType);
        Assert.True(skybox.Length > 1000);
    }

    [Fact]
    public void Same_object_matches_md5_etag_and_rejects_size_mismatch()
    {
        Assert.True(StorageSync.SameObject(16, "aabbcc", 16, "\"aabbcc\""));
        Assert.True(StorageSync.SameObject(16, "aabbcc", 16, "aabbcc"));
        Assert.False(StorageSync.SameObject(16, "aabbcc", 17, "\"aabbcc\""));
        Assert.False(StorageSync.SameObject(16, "aabbcc", 16, "\"deadbeef\""));
        Assert.True(StorageSync.SameObject(16, "aabbcc", 16, "\"aabbcc-1\""));
        Assert.False(StorageSync.SameObject(16, "aabbcc", null, "\"aabbcc\""));
    }

    [Fact]
    public void Cache_control_is_shorter_for_google_photos()
    {
        Assert.Equal(StorageSync.LongCacheControl, StorageSync.CacheControlFor("manufacturers/skybox.jpg"));
        Assert.Equal(StorageSync.LongCacheControl, StorageSync.CacheControlFor("legacy/assets/craig-lloyd.jpeg"));
        Assert.Equal(StorageSync.ShortCacheControl, StorageSync.CacheControlFor("gplace/12/1.jpg"));
        Assert.Equal(StorageSync.ShortCacheControl, StorageSync.CacheControlFor("google-cache/1/1.jpg"));
    }

    [Fact]
    public void Public_url_joins_cloudfront_and_key()
        => Assert.Equal(
            "https://dgo8ht4qmva4e.cloudfront.net/manufacturers/skybox.jpg",
            LocalMediaCatalog.PublicObjectUrl("https://dgo8ht4qmva4e.cloudfront.net/", "manufacturers/skybox.jpg"));

    [Fact]
    public void Provision_scripts_allow_bucket_root_not_only_uploads()
    {
        var root = FindRepoRoot();
        var sh = File.ReadAllText(Path.Combine(root, "provision-s3.sh"));
        var ps = File.ReadAllText(Path.Combine(root, "provision-s3.ps1"));
        Assert.Contains("arn:aws:s3:::${BUCKET}/*", sh);
        Assert.DoesNotContain("arn:aws:s3:::${BUCKET}/uploads/*", sh);
        Assert.Contains("arn:aws:s3:::${Bucket}/*", ps);
        Assert.DoesNotContain("/uploads/*", ps);
        Assert.Contains("put-user-policy", sh);
        Assert.Contains("storage sync", sh);
    }

    [Fact]
    public void Appsettings_does_not_embed_cloudfront_or_bucket_ids()
    {
        var json = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Ntca.Web", "appsettings.json"));
        Assert.DoesNotContain("dgo8ht4qmva4e", json);
        Assert.DoesNotContain("E3B0BMPLKILPJ3", json);
        Assert.DoesNotContain("ntca-tradingcardsassociation-media", json);
        Assert.Contains("\"Profile\": \"\"", json);
    }

    [Fact]
    public void Storage_profile_is_optional_and_non_secret()
    {
        var opt = new StorageOptions { Provider = "S3", Bucket = "ntca-tradingcardsassociation-media", Profile = "ntca-media-dev" };
        Assert.True(opt.UseS3);
        Assert.Equal("ntca-media-dev", opt.Profile);
    }

    [Fact]
    public void Skip_dotfiles_and_gitkeep()
    {
        Assert.True(LocalMediaCatalog.ShouldSkip(".gitkeep"));
        Assert.True(LocalMediaCatalog.ShouldSkip("gplace/.gitkeep"));
        Assert.False(LocalMediaCatalog.ShouldSkip("manufacturers/skybox.jpg"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "provision-s3.sh"))
                && Directory.Exists(Path.Combine(dir.FullName, "legacy-site")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }
}
