using Microsoft.AspNetCore.Mvc;

namespace Ntca.Web.Services;

public static class SiteMeta
{
    public const string SiteName = "National Trading Card Association";
    public const string DefaultDescription = "National Trading Card Association — shops, shows, manufacturers, and a beginner guide.";
    public const string DefaultImagePath = "/ntca-logo.png";
    public const int DefaultImageWidth = 512;
    public const int DefaultImageHeight = 512;

    public static string Origin(HttpRequest request)
        => $"{request.Scheme}://{request.Host}".TrimEnd('/');

    public static string Absolute(HttpRequest request, string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
            return Origin(request) + "/";
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var abs)
            && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return abs.ToString();
        if (!pathOrUrl.StartsWith('/'))
            pathOrUrl = "/" + pathOrUrl;
        return Origin(request) + pathOrUrl;
    }

    public static void Apply(
        Controller controller,
        string title,
        string description,
        string path,
        string ogType = "website",
        string? image = null,
        int? imageWidth = null,
        int? imageHeight = null,
        bool titleExact = false,
        string? jsonLd = null)
    {
        var req = controller.Request;
        controller.ViewData["Title"] = title;
        controller.ViewData["TitleExact"] = titleExact;
        controller.ViewData["Description"] = string.IsNullOrWhiteSpace(description) ? DefaultDescription : description;
        controller.ViewData["Canonical"] = Absolute(req, path);
        controller.ViewData["OgType"] = string.IsNullOrWhiteSpace(ogType) ? "website" : ogType;
        controller.ViewData["OgImage"] = string.IsNullOrWhiteSpace(image)
            ? Absolute(req, DefaultImagePath)
            : Absolute(req, image);
        controller.ViewData["OgImageWidth"] = imageWidth ?? DefaultImageWidth;
        controller.ViewData["OgImageHeight"] = imageHeight ?? DefaultImageHeight;
        if (!string.IsNullOrWhiteSpace(jsonLd))
            controller.ViewData["JsonLd"] = jsonLd;
    }
}
