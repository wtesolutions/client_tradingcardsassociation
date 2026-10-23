namespace Ntca.Web.Services;

public static class LegacyUrls
{
    public static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/about.html"] = "/about",
            ["/bloggers.html"] = "/bloggers",
            ["/contact.html"] = "/contact",
            ["/dealers.html"] = "/manufacturers",
            ["/directory.html"] = "/network",
            ["/email-autoresponder.html"] = "/",
            ["/events.html"] = "/events",
            ["/gallery.html"] = "/gallery",
            ["/grading-services.html"] = "/grading",
            ["/grading.html"] = "/grading",
            ["/history.html"] = "/history",
            ["/home.html"] = "/",
            ["/index.html"] = "/",
            ["/join.html"] = "/signup",
            ["/landing.html"] = "/",
            ["/manufacturers.html"] = "/manufacturers",
            ["/members-area.html"] = "/account",
            ["/members.html"] = "/account",
            ["/new-to-trading-cards.html"] = "/new-to-trading-cards",
            ["/news.html"] = "/news",
            ["/products.html"] = "/products",
            ["/retailers.html"] = "/shops",
            ["/shops.html"] = "/shops",
            ["/shows.html"] = "/calendar",
            ["/submit-event.html"] = "/shows/submit",
            ["/directory"] = "/network",
            ["/dealers"] = "/manufacturers",
            ["/retailers"] = "/shops",
            ["/home"] = "/",
            ["/landing"] = "/",
            ["/members"] = "/account",
            ["/members-area"] = "/account",
            ["/submit-event"] = "/shows/submit",
            ["/grading-services"] = "/grading",
            ["/join.html#signup"] = "/signup",
            ["/join.html#levels"] = "/signup",
            ["/join.html#involved"] = "/signup"
        };

    public static string? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var p = path.Trim();
        if (!p.StartsWith('/'))
            p = "/" + p;
        var q = p.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
            p = p[..q];
        var hash = p.IndexOf('#', StringComparison.Ordinal);
        var fragment = hash >= 0 ? p[hash..] : "";
        if (hash >= 0)
            p = p[..hash];
        if (Map.TryGetValue(p, out var dest))
            return dest + fragment;
        if (p.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            var stem = p[..^5];
            if (Map.TryGetValue(stem, out dest))
                return dest + fragment;
            if (Map.TryGetValue(p, out dest))
                return dest + fragment;
        }
        return null;
    }
}
