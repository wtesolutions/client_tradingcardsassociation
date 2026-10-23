using System.Text.Json;
using System.Text.Json.Nodes;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class PersonJsonLd
{
    public static string BuildPerson(PersonBio person, string origin, string canonicalUrl, string? imageUrl)
    {
        origin = origin.TrimEnd('/');
        var crumbs = new JsonArray
        {
            Crumb(1, origin + "/", "Home"),
            Crumb(2, origin + "/people", "People"),
            Crumb(3, canonicalUrl, person.FullName)
        };
        var personObj = new JsonObject
        {
            ["@type"] = "Person",
            ["@id"] = canonicalUrl + "#person",
            ["name"] = person.FullName,
            ["jobTitle"] = person.RoleTitle,
            ["description"] = FirstParagraph(person.BioText),
            ["url"] = canonicalUrl,
            ["worksFor"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = "National Trading Card Association",
                ["url"] = origin + "/"
            }
        };
        if (!string.IsNullOrWhiteSpace(imageUrl))
            personObj["image"] = imageUrl;
        var graph = new JsonArray
        {
            personObj,
            new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["@id"] = canonicalUrl + "#breadcrumb",
                ["itemListElement"] = crumbs
            }
        };
        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static string BuildFigure(IndustryFigure figure, string origin, string canonicalUrl, string? imageUrl)
    {
        origin = origin.TrimEnd('/');
        var crumbs = new JsonArray
        {
            Crumb(1, origin + "/", "Home"),
            Crumb(2, origin + "/people", "People"),
            Crumb(3, canonicalUrl, figure.FullName)
        };
        var personObj = new JsonObject
        {
            ["@type"] = "Person",
            ["@id"] = canonicalUrl + "#person",
            ["name"] = figure.FullName,
            ["url"] = canonicalUrl
        };
        if (!string.IsNullOrWhiteSpace(figure.RoleTitle))
            personObj["jobTitle"] = figure.RoleTitle;
        if (!string.IsNullOrWhiteSpace(figure.Summary))
            personObj["description"] = figure.Summary;
        if (!string.IsNullOrWhiteSpace(figure.WebsiteUrl))
            personObj["sameAs"] = new JsonArray { figure.WebsiteUrl };
        if (!string.IsNullOrWhiteSpace(imageUrl))
            personObj["image"] = imageUrl;
        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new JsonArray
            {
                personObj,
                new JsonObject
                {
                    ["@type"] = "BreadcrumbList",
                    ["@id"] = canonicalUrl + "#breadcrumb",
                    ["itemListElement"] = crumbs
                }
            }
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static string FirstParagraph(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var parts = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        return parts[0].Trim();
    }

    private static JsonObject Crumb(int pos, string url, string name) => new()
    {
        ["@type"] = "ListItem",
        ["position"] = pos,
        ["item"] = new JsonObject { ["@id"] = url, ["name"] = name }
    };
}
