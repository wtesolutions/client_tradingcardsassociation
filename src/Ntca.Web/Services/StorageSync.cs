namespace Ntca.Web.Services;

public static class StorageSync
{
    public const string LongCacheControl = "public, max-age=31536000";
    public const string ShortCacheControl = "public, max-age=86400";

    public static string CacheControlFor(string key)
    {
        var k = key.Replace('\\', '/').TrimStart('/');
        if (k.StartsWith("gplace/", StringComparison.OrdinalIgnoreCase)
            || k.StartsWith("google-cache/", StringComparison.OrdinalIgnoreCase))
            return ShortCacheControl;
        return LongCacheControl;
    }

    public static bool SameObject(long localLength, string localMd5Hex, long? remoteLength, string? remoteEtag)
    {
        if (remoteLength is null || remoteLength != localLength)
            return false;
        var etag = (remoteEtag ?? "").Trim().Trim('"');
        if (string.IsNullOrEmpty(etag) || etag.Contains('-', StringComparison.Ordinal))
            return true;
        return string.Equals(etag, localMd5Hex, StringComparison.OrdinalIgnoreCase);
    }
}
