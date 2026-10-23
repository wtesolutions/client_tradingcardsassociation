using System.Text.Json;
using System.Text.Json.Nodes;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class GalleryJsonLd
{
    public static string CategoryOf(GalleryItem item)
    {
        var hay = ((item.Caption ?? "") + " " + (item.Slug ?? "") + " " + (item.AltText ?? "")).ToLowerInvariant();
        if (ContainsAny(hay, "youth", "trade", "kid", "boys", "collector", "lucario"))
            return "Youth";
        if (ContainsAny(hay, "show", "booth", "jordan", "durham", "mebane", "ballpark", "hobby", "hall", "convention", "crowd"))
            return "Shows";
        if (ContainsAny(hay, "shop", "counter", "cardvault"))
            return "Shops";
        if (ContainsAny(hay, "grad", "slab", "display", "ohtani", "vintage"))
            return "Collections";
        return "Hobby";
    }

    public static string Build(IReadOnlyList<(GalleryItem Item, string Url)> images, string origin, string canonicalUrl)
    {
        origin = origin.TrimEnd('/');
        var imageNodes = new JsonArray();
        foreach (var (item, url) in images)
        {
            imageNodes.Add(new JsonObject
            {
                ["@type"] = "ImageObject",
                ["@id"] = canonicalUrl + "#" + item.Slug,
                ["name"] = item.Caption,
                ["caption"] = item.Caption,
                ["description"] = item.AltText ?? item.Caption,
                ["contentUrl"] = url,
                ["url"] = url
            });
        }
        var crumbs = new JsonArray
        {
            Crumb(1, origin + "/", "Home"),
            Crumb(2, canonicalUrl, "Gallery")
        };
        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new JsonArray
            {
                new JsonObject
                {
                    ["@type"] = "ImageGallery",
                    ["@id"] = canonicalUrl + "#gallery",
                    ["name"] = "NTCA Gallery",
                    ["url"] = canonicalUrl,
                    ["image"] = imageNodes
                },
                new JsonObject
                {
                    ["@type"] = "BreadcrumbList",
                    ["@id"] = canonicalUrl + "#breadcrumb",
                    ["itemListElement"] = crumbs
                }
            }
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static bool ContainsAny(string hay, params string[] needles)
        => needles.Any(n => hay.Contains(n, StringComparison.Ordinal));

    private static JsonObject Crumb(int pos, string url, string name) => new()
    {
        ["@type"] = "ListItem",
        ["position"] = pos,
        ["item"] = new JsonObject { ["@id"] = url, ["name"] = name }
    };
}
