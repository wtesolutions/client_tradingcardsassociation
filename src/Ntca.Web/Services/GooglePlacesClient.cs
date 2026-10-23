using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class GooglePlacesSkus
{
    public const string TextSearch = "TEXT_SEARCH";
    public const string PlaceDetails = "PLACE_DETAILS";
    public const string PlacePhoto = "PLACE_PHOTO";
}

public sealed record PlacesSearchHit(
    string PlaceId,
    string DisplayName,
    string FormattedAddress,
    decimal? Lat,
    decimal? Lng,
    string? GoogleMapsUri);

public sealed record PlacePhotoRef(string Name, string? AuthorName, string? AuthorUri);

public sealed record PlaceDetailsResult(
    string PlaceId,
    string? DisplayName,
    decimal? Rating,
    int? UserRatingCount,
    string? GoogleMapsUri,
    string? HoursJson,
    bool? OpenNow,
    decimal? Lat,
    decimal? Lng,
    IReadOnlyList<PlacePhotoRef> Photos);

public sealed record PlacesDetailFields(bool IncludeHours, bool IncludePhotos)
{
    public static PlacesDetailFields All { get; } = new(true, true);
}

public interface IGooglePlacesClient
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<PlacesSearchHit>> SearchTextAsync(string query, CancellationToken ct);
    Task<PlaceDetailsResult?> GetDetailsAsync(string placeId, PlacesDetailFields fields, CancellationToken ct);
    Task<byte[]?> GetPhotoAsync(string photoName, int maxWidthPx, CancellationToken ct);
}

public sealed class GooglePlacesClient : IGooglePlacesClient
{
    public const string SearchFieldMask = "places.id,places.displayName,places.formattedAddress,places.location";
    public const string DetailsCoreMask = "id,displayName,rating,userRatingCount,googleMapsUri,location";
    public const string DetailsHoursMask = "regularOpeningHours,currentOpeningHours.openNow";
    public const string DetailsPhotoMask = "photos.name,photos.authorAttributions";

    private readonly HttpClient _http;
    private readonly GooglePlacesOptions _opt;

    public GooglePlacesClient(HttpClient http, IOptions<GooglePlacesOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
        _http.BaseAddress ??= new Uri("https://places.googleapis.com/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool IsConfigured => _opt.HasKey && _opt.Enabled;

    public async Task<IReadOnlyList<PlacesSearchHit>> SearchTextAsync(string query, CancellationToken ct)
    {
        if (!IsConfigured)
            return [];
        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchText");
        ApplyKey(req);
        req.Headers.TryAddWithoutValidation("X-Goog-FieldMask", SearchFieldMask);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { textQuery = query, maxResultCount = 3, languageCode = "en" }),
            Encoding.UTF8,
            "application/json");
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var hits = new List<PlacesSearchHit>();
        if (!doc.RootElement.TryGetProperty("places", out var places))
            return hits;
        foreach (var p in places.EnumerateArray())
        {
            var id = Str(p, "id");
            if (string.IsNullOrEmpty(id))
                continue;
            hits.Add(new PlacesSearchHit(
                id,
                DisplayName(p),
                Str(p, "formattedAddress"),
                Coord(p, "latitude"),
                Coord(p, "longitude"),
                Str(p, "googleMapsUri")));
        }
        return hits;
    }

    public async Task<PlaceDetailsResult?> GetDetailsAsync(string placeId, PlacesDetailFields fields, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(placeId))
            return null;
        var mask = DetailsCoreMask;
        if (fields.IncludeHours)
            mask += "," + DetailsHoursMask;
        if (fields.IncludePhotos)
            mask += "," + DetailsPhotoMask;
        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/places/" + Uri.EscapeDataString(placeId));
        ApplyKey(req);
        req.Headers.TryAddWithoutValidation("X-Goog-FieldMask", mask);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var photos = new List<PlacePhotoRef>();
        if (root.TryGetProperty("photos", out var photoArr))
        {
            foreach (var ph in photoArr.EnumerateArray())
            {
                var name = Str(ph, "name");
                if (name.Length == 0)
                    continue;
                string? author = null, uri = null;
                if (ph.TryGetProperty("authorAttributions", out var attrs) && attrs.GetArrayLength() > 0)
                {
                    author = Str(attrs[0], "displayName");
                    uri = Str(attrs[0], "uri");
                }
                photos.Add(new PlacePhotoRef(name, author, uri));
            }
        }
        bool? openNow = null;
        if (root.TryGetProperty("currentOpeningHours", out var cur) && cur.TryGetProperty("openNow", out var on))
            openNow = on.ValueKind == JsonValueKind.True;
        string? hoursJson = null;
        if (root.TryGetProperty("regularOpeningHours", out var hours))
            hoursJson = hours.GetRawText();
        return new PlaceDetailsResult(
            Str(root, "id").Length > 0 ? Str(root, "id") : placeId,
            DisplayName(root),
            root.TryGetProperty("rating", out var rating) ? rating.GetDecimal() : null,
            root.TryGetProperty("userRatingCount", out var count) ? count.GetInt32() : null,
            Str(root, "googleMapsUri"),
            hoursJson,
            openNow,
            Coord(root, "latitude"),
            Coord(root, "longitude"),
            photos);
    }

    public async Task<byte[]?> GetPhotoAsync(string photoName, int maxWidthPx, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(photoName))
            return null;
        var path = "v1/" + photoName.TrimStart('/') + "/media?maxWidthPx=" + maxWidthPx + "&skipHttpRedirect=false";
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyKey(req);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var type = res.Content.Headers.ContentType?.MediaType ?? "";
        if (type.Contains("json", StringComparison.OrdinalIgnoreCase))
            return null;
        return await res.Content.ReadAsByteArrayAsync(ct);
    }

    private void ApplyKey(HttpRequestMessage req)
    {
        req.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _opt.ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static string DisplayName(JsonElement el)
    {
        if (el.TryGetProperty("displayName", out var dn))
            return Str(dn, "text");
        return "";
    }

    private static decimal? Coord(JsonElement el, string name)
    {
        if (!el.TryGetProperty("location", out var loc) || !loc.TryGetProperty(name, out var n))
            return null;
        return n.GetDecimal();
    }

    private static string Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
