using Ntca.Core;

namespace Ntca.Web.Services;

public sealed record LocalMediaObject(string Key, string FullPath, long Length, string ContentType);

public static class LocalMediaCatalog
{
    public static readonly string[] ProbeKeys =
    [
        "manufacturers/skybox.jpg",
        "legacy/assets/graded-slabs.jpeg",
        "legacy/assets/craig-lloyd.jpeg"
    ];

    public static IReadOnlyList<LocalMediaObject> Enumerate(string contentRoot, StorageOptions opt)
    {
        var items = new Dictionary<string, LocalMediaObject>(StringComparer.OrdinalIgnoreCase);
        var uploads = ResolveRoot(contentRoot, opt.LocalRoot, "wwwroot/uploads");
        AddTree(items, uploads, relativePrefix: "");

        foreach (var legacy in LegacyRoots(contentRoot, opt))
        {
            AddTree(items, Path.Combine(legacy, "assets"), relativePrefix: "legacy/assets");
            if (!Directory.Exists(legacy))
                continue;
            foreach (var file in Directory.GetFiles(legacy, "ntca-*", SearchOption.TopDirectoryOnly))
                AddFile(items, file, "legacy/" + Path.GetFileName(file));
        }

        return items.Values.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string PublicObjectUrl(string publicBaseUrl, string key)
        => publicBaseUrl.TrimEnd('/') + "/" + key.TrimStart('/');

    private static void AddTree(Dictionary<string, LocalMediaObject> items, string root, string relativePrefix)
    {
        if (!Directory.Exists(root))
            return;
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (ShouldSkip(rel))
                continue;
            var key = string.IsNullOrEmpty(relativePrefix)
                ? MediaPaths.ObjectKey(rel)
                : MediaPaths.ObjectKey(relativePrefix.TrimEnd('/') + "/" + rel);
            if (string.IsNullOrEmpty(key))
                continue;
            AddFile(items, file, key);
        }
    }

    private static void AddFile(Dictionary<string, LocalMediaObject> items, string fullPath, string key)
    {
        if (ShouldSkip(Path.GetFileName(fullPath)))
            return;
        var info = new FileInfo(fullPath);
        if (!info.Exists || info.Length <= 0)
            return;
        items[key] = new LocalMediaObject(key, info.FullName, info.Length, MediaPaths.ContentType(key));
    }

    public static bool ShouldSkip(string relative)
    {
        var name = Path.GetFileName(relative);
        if (string.IsNullOrEmpty(name) || name.StartsWith('.'))
            return true;
        return name is ".gitkeep" or ".DS_Store" or "Thumbs.db";
    }

    private static string ResolveRoot(string contentRoot, string? configured, string fallback)
    {
        var raw = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
        return Path.IsPathRooted(raw) ? raw : Path.GetFullPath(Path.Combine(contentRoot, raw));
    }

    private static IEnumerable<string> LegacyRoots(string contentRoot, StorageOptions opt)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(opt.LegacyRoot))
        {
            var configured = Path.IsPathRooted(opt.LegacyRoot)
                ? opt.LegacyRoot
                : Path.GetFullPath(Path.Combine(contentRoot, opt.LegacyRoot));
            if (seen.Add(configured))
                yield return configured;
        }
        foreach (var candidate in new[]
                 {
                     Path.GetFullPath(Path.Combine(contentRoot, "legacy-site")),
                     Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "legacy-site"))
                 })
        {
            if (seen.Add(candidate))
                yield return candidate;
        }
    }
}
