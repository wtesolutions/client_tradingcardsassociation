using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class StorageCli
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken ct = default)
    {
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var opt = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var verb = args.Length > 1 ? args[1].ToLowerInvariant() : "help";
        var dry = args.Any(a => a is "--dry-run" or "-n");
        return verb switch
        {
            "sync" => await SyncAsync(env, opt, services.GetService<IAmazonS3>(), dry, ct),
            "check" => await CheckAsync(env, opt, services.GetService<IAmazonS3>(), ct),
            _ => Help()
        };
    }

    internal static int Help()
    {
        Console.WriteLine("Usage: storage sync [--dry-run]");
        Console.WriteLine("       storage check");
        return 2;
    }

    internal static async Task<int> SyncAsync(
        IWebHostEnvironment env,
        StorageOptions opt,
        IAmazonS3? existing,
        bool dryRun,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opt.ResolvedBucket))
        {
            Console.Error.WriteLine("Storage:Bucket is required. Set it with dotnet user-secrets — do not put keys in appsettings.json.");
            return 2;
        }

        var items = LocalMediaCatalog.Enumerate(env.ContentRootPath, opt);
        Console.WriteLine($"storage sync  bucket={opt.ResolvedBucket}  region={opt.Region}  objects={items.Count}  dryRun={dryRun}");

        using var owned = existing is null ? S3ClientFactory.Create(opt) : null;
        var s3 = existing ?? owned!;
        var uploaded = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var item in items)
        {
            try
            {
                var remote = await TryHeadAsync(s3, opt.ResolvedBucket, item.Key, ct);
                if (remote is not null && StorageSync.SameObject(item.Length, Md5Hex(item.FullPath), remote.ContentLength, remote.ETag))
                {
                    Console.WriteLine($"skip  {item.Key}  ({item.Length} bytes)");
                    skipped++;
                    continue;
                }

                if (dryRun)
                {
                    Console.WriteLine($"upload {item.Key}  ({item.Length} bytes, {item.ContentType})");
                    uploaded++;
                    continue;
                }

                await using var stream = File.OpenRead(item.FullPath);
                var req = new PutObjectRequest
                {
                    BucketName = opt.ResolvedBucket,
                    Key = item.Key,
                    InputStream = stream,
                    ContentType = item.ContentType,
                    AutoCloseStream = false
                };
                req.Headers.CacheControl = StorageSync.CacheControlFor(item.Key);
                await s3.PutObjectAsync(req, ct);
                Console.WriteLine($"put   {item.Key}  ({item.Length} bytes)");
                uploaded++;
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"fail  {item.Key}  {ex.Message}");
            }
        }

        Console.WriteLine($"summary scanned={items.Count} uploaded={uploaded} skipped={skipped} failed={failed} dryRun={dryRun}");
        return failed == 0 ? 0 : 1;
    }

    internal static async Task<int> CheckAsync(
        IWebHostEnvironment env,
        StorageOptions opt,
        IAmazonS3? existing,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opt.ResolvedBucket))
        {
            Console.Error.WriteLine("Storage:Bucket is required. Set it with dotnet user-secrets.");
            return 2;
        }

        using var owned = existing is null ? S3ClientFactory.Create(opt) : null;
        var s3 = existing ?? owned!;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var failed = 0;
        var local = LocalMediaCatalog.Enumerate(env.ContentRootPath, opt)
            .ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"storage check  bucket={opt.ResolvedBucket}  publicBase={opt.PublicBaseUrl}");
        foreach (var key in LocalMediaCatalog.ProbeKeys)
        {
            var s3Ok = false;
            var httpOk = false;
            var detail = new List<string>();
            try
            {
                var meta = await TryHeadAsync(s3, opt.ResolvedBucket, key, ct);
                if (meta is null)
                {
                    detail.Add("s3=missing");
                }
                else
                {
                    s3Ok = true;
                    detail.Add("s3=" + meta.ContentLength + " " + meta.Headers.ContentType);
                    if (local.TryGetValue(key, out var item)
                        && !StorageSync.SameObject(item.Length, Md5Hex(item.FullPath), meta.ContentLength, meta.ETag))
                    {
                        s3Ok = false;
                        detail.Add("s3-size/etag-mismatch");
                    }
                }
            }
            catch (Exception ex)
            {
                detail.Add("s3-error=" + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(opt.PublicBaseUrl))
            {
                detail.Add("http=skip (Storage:PublicBaseUrl empty)");
                httpOk = s3Ok;
            }
            else
            {
                var url = LocalMediaCatalog.PublicObjectUrl(opt.PublicBaseUrl, key);
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, url);
                    using var resp = await http.SendAsync(req, ct);
                    httpOk = resp.IsSuccessStatusCode;
                    detail.Add("http=" + (int)resp.StatusCode + " " + url);
                }
                catch (Exception ex)
                {
                    detail.Add("http-error=" + ex.Message + " " + url);
                }
            }

            var pass = s3Ok && httpOk;
            if (!pass)
                failed++;
            Console.WriteLine($"{(pass ? "PASS" : "FAIL")}  {key}  {string.Join("  ", detail)}");
        }

        Console.WriteLine($"summary probes={LocalMediaCatalog.ProbeKeys.Length} failed={failed}");
        return failed == 0 ? 0 : 1;
    }

    private static async Task<GetObjectMetadataResponse?> TryHeadAsync(IAmazonS3 s3, string bucket, string key, CancellationToken ct)
    {
        try
        {
            return await s3.GetObjectMetadataAsync(bucket, key, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string Md5Hex(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(MD5.HashData(fs)).ToLowerInvariant();
    }
}
