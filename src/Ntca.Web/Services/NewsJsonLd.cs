using System.Text.Json;
using System.Text.Json.Nodes;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class NewsJsonLd
{
    public static string BuildPost(Post post, string origin, string canonical, string? imageUrl)
    {
        origin = origin.TrimEnd('/');
        var node = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["headline"] = post.Title,
            ["url"] = canonical,
            ["mainEntityOfPage"] = canonical,
            ["publisher"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = SiteMeta.SiteName,
                ["url"] = origin + "/"
            }
        };
        if (!string.IsNullOrWhiteSpace(post.Excerpt))
            node["description"] = post.Excerpt.Trim();
        if (post.PublishedAt is DateTime published)
            node["datePublished"] = Iso(published);
        var modified = post.UpdatedAt == default ? post.PublishedAt : post.UpdatedAt;
        if (modified is DateTime mod)
            node["dateModified"] = Iso(mod);
        if (!string.IsNullOrWhiteSpace(post.AuthorName))
        {
            node["author"] = new JsonObject
            {
                ["@type"] = "Person",
                ["name"] = post.AuthorName.Trim()
            };
        }
        if (!string.IsNullOrWhiteSpace(imageUrl))
            node["image"] = imageUrl;
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static string BuildIndex(string origin, IReadOnlyList<Post> posts)
    {
        origin = origin.TrimEnd('/');
        var canonical = origin + "/news";
        var items = new JsonArray();
        var pos = 1;
        foreach (var post in posts)
        {
            items.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = pos,
                ["name"] = post.Title,
                ["url"] = origin + "/news/" + post.Slug
            });
            pos++;
        }
        var graph = new JsonArray
        {
            new JsonObject
            {
                ["@type"] = "ItemList",
                ["@id"] = canonical + "#list",
                ["name"] = "News from the National Trading Card Association",
                ["url"] = canonical,
                ["numberOfItems"] = posts.Count
            },
            new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["@id"] = canonical + "#breadcrumb",
                ["itemListElement"] = new JsonArray
                {
                    Crumb(1, origin + "/", "Home"),
                    Crumb(2, canonical, "News")
                }
            }
        };
        if (items.Count > 0)
            graph[0]!.AsObject()["itemListElement"] = items;
        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static IReadOnlyList<string> ValidatePost(string json)
    {
        var errors = new List<string>();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            return ["JSON-LD is not valid JSON: " + ex.Message];
        }
        if (root is not JsonObject obj)
            return ["JSON-LD root must be an object."];
        if (obj["@context"]?.GetValue<string>() != "https://schema.org")
            errors.Add("@context must be https://schema.org.");
        if (obj["@type"]?.GetValue<string>() != "BlogPosting")
            errors.Add("@type must be BlogPosting.");
        if (obj["headline"] is not JsonValue h || !h.TryGetValue<string>(out var title) || string.IsNullOrWhiteSpace(title))
            errors.Add("BlogPosting.headline is required.");
        if (obj["url"] is not JsonValue u || !u.TryGetValue<string>(out var url) || string.IsNullOrWhiteSpace(url))
            errors.Add("BlogPosting.url is required.");
        return errors;
    }

    private static JsonObject Crumb(int pos, string url, string name) => new()
    {
        ["@type"] = "ListItem",
        ["position"] = pos,
        ["name"] = name,
        ["item"] = url
    };

    private static string Iso(DateTime value)
        => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
}
