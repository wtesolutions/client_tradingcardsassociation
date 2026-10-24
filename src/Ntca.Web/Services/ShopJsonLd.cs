using System.Text.Json;
using System.Text.Json.Nodes;
using Ntca.Core;

namespace Ntca.Web.Services;

public sealed class ShopSchemaInput
{
    public required Shop Shop { get; init; }
    public required string Origin { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string AiSummary { get; init; }
    public string? TierName { get; init; }
    public IReadOnlyList<ShopHours> Hours { get; init; } = [];
    public IReadOnlyList<ShopEvent> UpcomingEvents { get; init; } = [];
    public int ApprovedReviewCount { get; init; }
    public decimal? AverageRating { get; init; }
    public IReadOnlyList<ShopServiceOffer> Offers { get; init; } = [];
    public IReadOnlyList<string> SameAs { get; init; } = [];
    public string? ImageUrl { get; init; }
    public string? LogoUrl { get; init; }
    public string? PriceRange { get; init; }
}

public sealed class ShopServiceOffer
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

public static class ShopJsonLd
{
    public static string BuildStateHub(
        string origin,
        string stateCode,
        int shopCount,
        IReadOnlyList<(string Name, string Url)>? shops = null)
    {
        origin = origin.TrimEnd('/');
        var name = ShopCopy.StateName(stateCode);
        var code = stateCode.Trim().ToLowerInvariant();
        var canonical = origin + "/shops/" + code;
        return BuildListing(
            origin,
            canonical,
            "Hobby shops in " + name,
            shopCount + " active listings in " + name + ".",
            shops ?? [],
            [
                (1, origin + "/", "Home"),
                (2, origin + "/shops", "Shops"),
                (3, canonical, name)
            ],
            shopCount);
    }

    public static string BuildListing(
        string origin,
        string canonical,
        string name,
        string description,
        IReadOnlyList<(string Name, string Url)> shops,
        IReadOnlyList<(int Position, string Url, string Name)> crumbs,
        int? numberOfItems = null)
    {
        origin = origin.TrimEnd('/');
        canonical = canonical.TrimEnd('/');
        var listId = canonical + "#list";
        var crumbId = canonical + "#breadcrumb";
        var items = new JsonArray();
        var pos = 1;
        foreach (var shop in shops)
        {
            items.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = pos,
                ["name"] = shop.Name,
                ["url"] = shop.Url
            });
            pos++;
        }
        var list = new JsonObject
        {
            ["@type"] = "ItemList",
            ["@id"] = listId,
            ["name"] = name,
            ["url"] = canonical
        };
        if (!string.IsNullOrWhiteSpace(description))
            list["description"] = description;
        var count = numberOfItems ?? shops.Count;
        if (count > 0)
            list["numberOfItems"] = count;
        if (items.Count > 0)
            list["itemListElement"] = items;
        var crumbItems = new JsonArray();
        foreach (var crumb in crumbs)
            crumbItems.Add(Crumb(crumb.Position, crumb.Url, crumb.Name));
        var graph = new JsonArray
        {
            list,
            new JsonObject
            {
                ["@type"] = "BreadcrumbList",
                ["@id"] = crumbId,
                ["itemListElement"] = crumbItems
            }
        };
        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static IReadOnlyList<string> ValidateListing(string json)
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
        if (obj["@graph"] is not JsonArray graph || graph.Count == 0)
            return ["@graph must be a non-empty array."];
        JsonObject? list = null;
        JsonObject? crumbs = null;
        foreach (var item in graph)
        {
            if (item is not JsonObject node)
                continue;
            var type = TypeOf(node);
            if (type == "ItemList")
                list = node;
            else if (type == "BreadcrumbList")
                crumbs = node;
        }
        if (list is null)
            errors.Add("Listing graph must include ItemList.");
        else if (!HasText(list, "name") || !HasText(list, "url"))
            errors.Add("ItemList requires name and url.");
        if (crumbs is null)
            errors.Add("Listing graph must include BreadcrumbList.");
        if (json.Contains("aggregateRating", StringComparison.Ordinal))
            errors.Add("Listing JSON-LD must not emit aggregateRating.");
        return errors;
    }

    public static string Build(ShopSchemaInput input)
    {
        var shop = input.Shop;
        var origin = input.Origin.TrimEnd('/');
        var canonical = input.CanonicalUrl;
        var storeId = canonical + "#store";
        var orgId = origin + "/#org";
        var pageId = canonical + "#webpage";
        var crumbId = canonical + "#breadcrumb";

        var graph = new JsonArray
        {
            BuildOrg(orgId, origin),
            BuildStore(input, storeId, orgId, origin),
            BuildWebPage(input, pageId, storeId, crumbId),
            BuildBreadcrumb(shop, origin, canonical, crumbId)
        };

        foreach (var ev in input.UpcomingEvents)
        {
            var node = BuildEvent(ev, origin, storeId, shop);
            if (node is not null)
                graph.Add(node);
        }

        var root = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static IReadOnlyList<string> Validate(string json)
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
        if (obj["@graph"] is not JsonArray graph || graph.Count == 0)
        {
            errors.Add("@graph must be a non-empty array.");
            return errors;
        }

        JsonObject? store = null;
        JsonObject? page = null;
        JsonObject? crumbs = null;
        foreach (var item in graph)
        {
            if (item is not JsonObject node)
            {
                errors.Add("@graph entry is not an object.");
                continue;
            }
            AssertNoEmpty(node, "", errors);
            var type = TypeOf(node);
            if (IsStoreType(node))
                store = node;
            else if (type == "WebPage")
                page = node;
            else if (type == "BreadcrumbList")
                crumbs = node;
            else if (type == "Event")
                ValidateEvent(node, errors);
            else if (type == "Organization")
            {
                if (!HasText(node, "name"))
                    errors.Add("Organization is missing name.");
            }
        }

        if (store is null)
            errors.Add("Graph must include Store, HobbyShop, or SportingGoodsStore.");
        else
            ValidateStore(store, errors);

        if (page is null)
            errors.Add("Graph must include WebPage.");
        else
        {
            if (!HasText(page, "dateModified"))
                errors.Add("WebPage.dateModified is required.");
            if (page["speakable"] is null)
                errors.Add("WebPage.speakable is required.");
        }

        if (crumbs is null)
            errors.Add("Graph must include BreadcrumbList.");
        else
            ValidateBreadcrumb(crumbs, errors);

        return errors;
    }

    private static JsonObject BuildOrg(string orgId, string origin) => new()
    {
        ["@type"] = "Organization",
        ["@id"] = orgId,
        ["name"] = "National Trading Card Association",
        ["url"] = origin + "/",
        ["alternateName"] = "NTCA"
    };

    private static JsonObject BuildStore(ShopSchemaInput input, string storeId, string orgId, string origin)
    {
        var shop = input.Shop;
        var store = new JsonObject
        {
            ["@type"] = StoreTypes(shop),
            ["@id"] = storeId,
            ["additionalType"] = "trading card shop",
            ["name"] = shop.Name,
            ["url"] = input.CanonicalUrl
        };
        Set(store, "description", input.AiSummary);
        var sameAs = SameAs(input);
        if (sameAs.Count > 0)
            store["sameAs"] = sameAs;
        Set(store, "telephone", shop.Phone);
        Set(store, "email", shop.PublicEmail);
        var address = Address(shop);
        if (address is not null)
            store["address"] = address;
        if (shop.Latitude is { } lat && shop.Longitude is { } lng)
        {
            store["geo"] = new JsonObject
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = (double)lat,
                ["longitude"] = (double)lng
            };
        }
        var hours = HoursSpec(input.Hours);
        if (hours is not null)
            store["openingHoursSpecification"] = hours;
        Set(store, "priceRange", input.PriceRange);
        Set(store, "image", input.ImageUrl);
        Set(store, "logo", input.LogoUrl);
        if (ShopCopy.HasMappableAddress(shop))
            store["hasMap"] = ShopCopy.MapsUrl(shop);
        var area = AreaServed(shop);
        if (area is not null)
            store["areaServed"] = area;
        var knows = new JsonArray();
        foreach (var k in ShopCopy.KnowsAbout(shop.PrimarySpecialty))
            knows.Add(k);
        store["knowsAbout"] = knows;

        if (input.Offers.Count > 0)
        {
            var offers = new JsonArray();
            var items = new JsonArray();
            foreach (var o in input.Offers)
            {
                var offer = new JsonObject
                {
                    ["@type"] = "Offer",
                    ["itemOffered"] = new JsonObject
                    {
                        ["@type"] = "Service",
                        ["name"] = o.Name
                    }
                };
                if (!string.IsNullOrWhiteSpace(o.Description))
                    ((JsonObject)offer["itemOffered"]!)["description"] = o.Description;
                offers.Add(offer);
                items.Add(new JsonObject
                {
                    ["@type"] = "Offer",
                    ["itemOffered"] = new JsonObject { ["@type"] = "Service", ["name"] = o.Name }
                });
            }
            store["makesOffer"] = offers;
            store["hasOfferCatalog"] = new JsonObject
            {
                ["@type"] = "OfferCatalog",
                ["name"] = shop.Name + " services",
                ["itemListElement"] = items
            };
        }

        var member = new JsonObject
        {
            ["@type"] = "Organization",
            ["@id"] = orgId,
            ["name"] = "National Trading Card Association"
        };
        Set(member, "description", string.IsNullOrWhiteSpace(input.TierName) ? null : "NTCA listing tier: " + input.TierName);
        store["memberOf"] = member;

        // First-party ShopReview only. Never emit Google Places ratings/reviews as
        // schema.org aggregateRating or Review — Google Search review-snippet
        // guidelines disallow third-party / self-serving review markup.
        // https://developers.google.com/search/docs/appearance/structured-data/review-snippet
        if (input.ApprovedReviewCount > 0 && input.AverageRating is { } rating)
        {
            store["aggregateRating"] = new JsonObject
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = Math.Round(rating, 2),
                ["reviewCount"] = input.ApprovedReviewCount,
                ["bestRating"] = 5,
                ["worstRating"] = 1
            };
        }

        store["potentialAction"] = new JsonObject
        {
            ["@type"] = "ViewAction",
            ["target"] = input.CanonicalUrl,
            ["name"] = "View shop listing"
        };
        return store;
    }

    private static JsonObject BuildWebPage(ShopSchemaInput input, string pageId, string storeId, string crumbId)
    {
        var page = new JsonObject
        {
            ["@type"] = "WebPage",
            ["@id"] = pageId,
            ["url"] = input.CanonicalUrl,
            ["name"] = input.Shop.Name,
            ["dateModified"] = input.Shop.UpdatedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["about"] = new JsonObject { ["@id"] = storeId },
            ["breadcrumb"] = new JsonObject { ["@id"] = crumbId },
            ["speakable"] = new JsonObject
            {
                ["@type"] = "SpeakableSpecification",
                ["cssSelector"] = new JsonArray { ".shop-name", ".shop-ai-summary" }
            }
        };
        Set(page, "description", input.AiSummary);
        return page;
    }

    private static JsonObject BuildBreadcrumb(Shop shop, string origin, string canonical, string crumbId)
    {
        var statePath = origin + "/shops/" + shop.StateCode.ToLowerInvariant();
        var items = new JsonArray
        {
            Crumb(1, origin + "/", "Home"),
            Crumb(2, origin + "/shops", "Shops"),
            Crumb(3, statePath, ShopCopy.StateName(shop.StateCode)),
            Crumb(4, statePath + "/" + ShopCopy.CitySlug(shop.City), shop.City),
            Crumb(5, canonical, shop.Name)
        };
        return new JsonObject
        {
            ["@type"] = "BreadcrumbList",
            ["@id"] = crumbId,
            ["itemListElement"] = items
        };
    }

    private static JsonObject Crumb(int pos, string url, string name) => new()
    {
        ["@type"] = "ListItem",
        ["position"] = pos,
        ["name"] = name,
        ["item"] = url
    };

    private static JsonObject? BuildEvent(ShopEvent ev, string origin, string storeId, Shop shop)
    {
        if (string.IsNullOrWhiteSpace(ev.Title))
            return null;
        var loc = CalendarControllerLoc(ev);
        var node = new JsonObject
        {
            ["@type"] = "Event",
            ["@id"] = origin + loc + "#event",
            ["name"] = ev.Title,
            ["startDate"] = ev.StartsAt.ToString("yyyy-MM-dd"),
            ["eventAttendanceMode"] = "https://schema.org/OfflineEventAttendanceMode",
            ["eventStatus"] = ev.Status == "POSTPONED"
                ? "https://schema.org/EventPostponed"
                : "https://schema.org/EventScheduled",
            ["location"] = new JsonObject { ["@id"] = storeId }
        };
        Set(node, "description", ev.Description);
        Set(node, "url", origin + loc);
        if (ev.EndsAt is DateTime end)
            node["endDate"] = end.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(ev.TicketUrl))
        {
            node["offers"] = new JsonObject
            {
                ["@type"] = "Offer",
                ["url"] = ev.TicketUrl,
                ["availability"] = "https://schema.org/InStock"
            };
        }
        return node;
    }

    private static string CalendarControllerLoc(ShopEvent e)
    {
        var st = (e.StateCode ?? e.Venue?.StateCode ?? e.Shop?.StateCode ?? "xx").ToLowerInvariant();
        var city = (e.City ?? e.Venue?.City ?? e.Shop?.City ?? "city").ToLowerInvariant().Replace(' ', '-').Replace(".", "");
        return $"/shows/{st}/{city}/{e.Slug}";
    }

    private static JsonArray SameAs(ShopSchemaInput input)
    {
        var urls = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in input.SameAs)
            AddUrl(urls, url, seen);
        if (urls.Count == 0)
        {
            foreach (var url in ShopSocialLinks.SameAs(input.Shop))
                AddUrl(urls, url, seen);
        }
        return urls;
    }

    private static JsonObject? Address(Shop shop)
    {
        if (string.IsNullOrWhiteSpace(shop.AddressLine1)
            && string.IsNullOrWhiteSpace(shop.City)
            && string.IsNullOrWhiteSpace(shop.StateCode))
            return null;
        var addr = new JsonObject { ["@type"] = "PostalAddress" };
        Set(addr, "streetAddress", JoinStreet(shop.AddressLine1, shop.AddressLine2));
        Set(addr, "addressLocality", shop.City);
        Set(addr, "addressRegion", shop.StateCode);
        Set(addr, "postalCode", shop.PostalCode);
        Set(addr, "addressCountry", shop.CountryCode);
        return addr;
    }

    private static JsonObject? AreaServed(Shop shop)
    {
        if (string.IsNullOrWhiteSpace(shop.City))
            return null;
        var city = new JsonObject
        {
            ["@type"] = "City",
            ["name"] = shop.City
        };
        if (!string.IsNullOrWhiteSpace(shop.StateCode))
        {
            city["containedInPlace"] = new JsonObject
            {
                ["@type"] = "State",
                ["name"] = ShopCopy.StateName(shop.StateCode),
                ["addressCountry"] = shop.CountryCode
            };
        }
        return city;
    }

    private static JsonArray? HoursSpec(IReadOnlyList<ShopHours> hours)
    {
        var open = hours
            .Where(h => !h.IsClosed && h.OpensAt is not null && h.ClosesAt is not null)
            .OrderBy(h => h.DayOfWeek)
            .ToList();
        if (open.Count == 0)
            return null;
        var arr = new JsonArray();
        foreach (var h in open)
        {
            arr.Add(new JsonObject
            {
                ["@type"] = "OpeningHoursSpecification",
                ["dayOfWeek"] = ShopCopy.SchemaDay(h.DayOfWeek),
                ["opens"] = h.OpensAt!.Value.ToString("HH:mm"),
                ["closes"] = h.ClosesAt!.Value.ToString("HH:mm")
            });
        }
        return arr;
    }

    private static void ValidateStore(JsonObject store, List<string> errors)
    {
        if (!HasText(store, "name"))
            errors.Add("Store.name is required.");
        if (!HasText(store, "@id") || store["@id"]?.GetValue<string>()?.EndsWith("#store", StringComparison.Ordinal) != true)
            errors.Add("Store.@id must end with #store.");
        if (!HasText(store, "additionalType"))
            errors.Add("Store.additionalType (trading card shop) is required.");
        if (store["aggregateRating"] is not null)
        {
            var ar = store["aggregateRating"] as JsonObject;
            var count = ar?["reviewCount"]?.GetValue<int>() ?? 0;
            if (count <= 0)
                errors.Add("aggregateRating must not be emitted without real reviews.");
        }
        if (store["geo"] is JsonObject geo)
        {
            if (geo["latitude"] is null || geo["longitude"] is null)
                errors.Add("GeoCoordinates requires latitude and longitude.");
        }
        if (store["address"] is JsonObject addr)
        {
            if (!HasText(addr, "addressLocality") && !HasText(addr, "streetAddress"))
                errors.Add("PostalAddress needs streetAddress or addressLocality.");
        }
        if (store["openingHoursSpecification"] is JsonArray hours)
        {
            foreach (var h in hours)
            {
                if (h is not JsonObject spec || !HasText(spec, "dayOfWeek") || !HasText(spec, "opens") || !HasText(spec, "closes"))
                    errors.Add("OpeningHoursSpecification requires dayOfWeek, opens, and closes.");
            }
        }
        if (store["memberOf"] is not JsonObject member || member["@id"]?.GetValue<string>()?.EndsWith("#org", StringComparison.Ordinal) != true)
            errors.Add("Store.memberOf must reference the NTCA Organization #org.");
    }

    private static void ValidateEvent(JsonObject node, List<string> errors)
    {
        if (!HasText(node, "name") || !HasText(node, "startDate"))
            errors.Add("Event requires name and startDate.");
    }

    private static void ValidateBreadcrumb(JsonObject crumbs, List<string> errors)
    {
        if (crumbs["itemListElement"] is not JsonArray items || items.Count < 5)
        {
            errors.Add("BreadcrumbList must be Home > Shops > State > City > Shop.");
            return;
        }
        var names = items.OfType<JsonObject>().Select(i => i["name"]?.GetValue<string>()).ToList();
        if (names[0] != "Home" || names[1] != "Shops")
            errors.Add("BreadcrumbList must start with Home > Shops.");
    }

    private static void AssertNoEmpty(JsonNode node, string path, List<string> errors)
    {
        switch (node)
        {
            case JsonValue v when v.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s):
                errors.Add("Empty string at " + (path.Length == 0 ? "(root)" : path));
                break;
            case JsonArray arr:
                if (arr.Count == 0)
                    errors.Add("Empty array at " + path);
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonNode child)
                        AssertNoEmpty(child, path + "[" + i + "]", errors);
                }
                break;
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (kv.Value is JsonNode child)
                        AssertNoEmpty(child, path + "." + kv.Key, errors);
                }
                break;
        }
    }

    private static JsonArray StoreTypes(Shop shop)
    {
        var types = new JsonArray { "LocalBusiness", "Store" };
        var specific = ShopCopy.StoreType(shop);
        if (specific is "HobbyShop" or "SportingGoodsStore")
            types.Add(specific);
        return types;
    }

    private static bool IsStoreType(JsonObject node)
        => TypesOf(node).Any(t => t is "LocalBusiness" or "Store" or "HobbyShop" or "SportingGoodsStore");

    private static IEnumerable<string> TypesOf(JsonObject node)
    {
        var t = node["@type"];
        if (t is JsonValue v)
        {
            yield return v.GetValue<string>();
            yield break;
        }
        if (t is JsonArray a)
        {
            foreach (var item in a)
            {
                if (item is JsonValue iv)
                    yield return iv.GetValue<string>();
            }
        }
    }

    private static string? TypeOf(JsonObject node)
    {
        var types = TypesOf(node).ToList();
        foreach (var t in types)
        {
            if (t is "HobbyShop" or "SportingGoodsStore" or "Store")
                return t;
        }
        return types.FirstOrDefault();
    }

    private static bool HasText(JsonObject obj, string name)
        => obj[name] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s);

    private static void Set(JsonObject obj, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            obj[name] = value.Trim();
    }

    private static void AddUrl(JsonArray arr, string? url, HashSet<string>? seen = null)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            return;
        if (seen is not null && !seen.Add(url))
            return;
        arr.Add(url);
    }

    private static string? JoinStreet(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b))
            return null;
        if (string.IsNullOrWhiteSpace(b))
            return a;
        if (string.IsNullOrWhiteSpace(a))
            return b;
        return a + ", " + b;
    }
}
