using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Ntca.Core;

namespace Ntca.Web.Services;

public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly StorageOptions _opt;

    public S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> opt)
    {
        _s3 = s3;
        _opt = opt.Value;
    }

    public bool UsesObjectStorage => true;

    public string PublicUrl(string relativePath)
    {
        var key = MediaPaths.ObjectKey(relativePath);
        if (!string.IsNullOrWhiteSpace(_opt.PublicBaseUrl))
            return _opt.PublicBaseUrl.TrimEnd('/') + "/" + key.TrimStart('/');
        return "/media/" + key.TrimStart('/');
    }

    public async Task<string> SaveAsync(string relativePath, Stream content, string contentType, CancellationToken ct = default)
    {
        var key = MediaPaths.ObjectKey(relativePath);
        var req = new PutObjectRequest
        {
            BucketName = _opt.ResolvedBucket,
            Key = key,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? MediaPaths.ContentType(key) : contentType,
            AutoCloseStream = false
        };
        await _s3.PutObjectAsync(req, ct);
        return PublicUrl(relativePath);
    }

    public async Task<bool> ExistsAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(_opt.ResolvedBucket, MediaPaths.ObjectKey(relativePath), ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        await _s3.DeleteObjectAsync(_opt.ResolvedBucket, MediaPaths.ObjectKey(relativePath), ct);
    }

    public async Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _s3.GetObjectAsync(_opt.ResolvedBucket, MediaPaths.ObjectKey(relativePath), ct);
            var ms = new MemoryStream();
            await resp.ResponseStream.CopyToAsync(ms, ct);
            ms.Position = 0;
            return ms;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
