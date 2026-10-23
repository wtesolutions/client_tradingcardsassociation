namespace Ntca.Web.Services;

public static class MediaPaths
{
    public static string ObjectKey(string? relativePath)
    {
        var p = (relativePath ?? "").Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrEmpty(p))
            return "";
        if (p.StartsWith("wwwroot/uploads/", StringComparison.OrdinalIgnoreCase))
            p = p["wwwroot/uploads/".Length..];
        if (p.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            p = p["uploads/".Length..];
        if (p.StartsWith("legacy-site/", StringComparison.OrdinalIgnoreCase))
            p = p["legacy-site/".Length..];
        if (p.StartsWith("legacy/", StringComparison.OrdinalIgnoreCase))
            return p;
        if (p.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("ntca-", StringComparison.OrdinalIgnoreCase))
            return "legacy/" + p;
        return p;
    }

    public static string LegacyRelative(string? relativePath)
    {
        var key = ObjectKey(relativePath);
        if (key.StartsWith("legacy/", StringComparison.OrdinalIgnoreCase))
            return key[7..];
        return key;
    }

    public static string ContentType(string? path)
    {
        var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}
