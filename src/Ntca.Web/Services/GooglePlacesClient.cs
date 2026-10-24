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
    string? GoogleMapsUri,
    string? NationalPhone = null);

public sealed record PlacePhotoRef(string Name, string? AuthorName, string? AuthorUri);

public sealed record PlaceReviewRef(
    string? AuthorName,
    string? AuthorUri,
    decimal? Rating,
    string? Text,
    string? RelativeTime,
    DateTime? PublishTime,
    string? AttributionUri);

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

public sealed record PlaceDetailsRich(
    string PlaceId,
    string? DisplayName,
    decimal? Rating,
    int? UserRatingCount,
    string? GoogleMapsUri,
    string? HoursJson,
    string? CurrentHoursJson,
    bool? OpenNow,
    decimal? Lat,
    decimal? Lng,
    string? FormattedAddress,
    string? WebsiteUri,
    string? NationalPhone,
    string? InternationalPhone,
    string? BusinessStatus,
    string? EditorialSummary,
    string? TypesJson,
    string? PrimaryType,
    string? PriceLevel,
    string? PlusCode,
    string? AttributesJson,
    IReadOnlyList<PlacePhotoRef> Photos,
    IReadOnlyList<PlaceReviewRef> Reviews);

public sealed record PlacesDetailFields(bool IncludeHours, bool IncludePhotos)
{
    public static PlacesDetailFields All { get; } = new(true, true);
}

public interface IGooglePlacesClient
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<PlacesSearchHit>> SearchTextAsync(string query, CancellationToken ct);
    Task<IReadOnlyList<PlacesSearchHit>> SearchTextNearAsync(
        string query, decimal? lat, decimal? lng, int radiusMeters, CancellationToken ct);
    Task<PlaceDetailsResult?> GetDetailsAsync(string placeId, PlacesDetailFields fields, CancellationToken ct);
    Task<PlaceDetailsRich?> GetDetailsRichAsync(string placeId, CancellationToken ct);
    Task<byte[]?> GetPhotoAsync(string photoName, int maxWidthPx, CancellationToken ct);
}

public sealed class GooglePlacesClient : IGooglePlacesClient
{
    public const string SearchFieldMask = "places.id,places.displayName,places.formattedAddress,places.location";
    public const string MatchFieldMask = "places.id,places.displayName,places.formattedAddress,places.location,places.nationalPhoneNumber,places.googleMapsUri";
    public const string DetailsCoreMask = "id,displayName,rating,userRatingCount,googleMapsUri,location";
    public const string DetailsHoursMask = "regularOpeningHours,currentOpeningHours.openNow";
    public const string DetailsPhotoMask = "photos.name,photos.authorAttributions";
    public const string DetailsRichMask =
        "id,displayName,rating,userRatingCount,reviews,regularOpeningHours,currentOpeningHours," +
        "businessStatus,websiteUri,nationalPhoneNumber,internationalPhoneNumber,googleMapsUri," +
        "formattedAddress,location,photos,editorialSummary,types,primaryType,priceLevel,plusCode," +
        "parkingOptions,paymentOptions,accessibilityOptions";

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

    public async Task<IReadOnlyList<PlacesSearchHit>> SearchTextNearAsync(
        string query, decimal? lat, decimal? lng, int radiusMeters, CancellationToken ct)
    {
        if (!IsConfigured)
            return [];
        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchText");
        ApplyKey(req);
        req.Headers.TryAddWithoutValidation("X-Goog-FieldMask", MatchFieldMask);
        object body;
        if (lat is { } la && lng is { } ln && radiusMeters > 0)
        {
            body = new
            {
                textQuery = query,
                pageSize = 5,
                languageCode = "en",
                locationBias = new
                {
                    circle = new
                    {
                        center = new { latitude = la, longitude = ln },
                        radius = radiusMeters
                    }
                }
            };
        }
        else
        {
            body = new { textQuery = query, pageSize = 5, languageCode = "en" };
        }
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
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
                Str(p, "googleMapsUri"),
                Str(p, "nationalPhoneNumber")));
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

    public async Task<PlaceDetailsRich?> GetDetailsRichAsync(string placeId, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(placeId))
            return null;
        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/places/" + Uri.EscapeDataString(placeId));
        ApplyKey(req);
        req.Headers.TryAddWithoutValidation("X-Goog-FieldMask", DetailsRichMask);
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;
        var photos = ParsePhotos(root);
        var reviews = ParseReviews(root);
        bool? openNow = null;
        if (root.TryGetProperty("currentOpeningHours", out var cur) && cur.TryGetProperty("openNow", out var on))
            openNow = on.ValueKind == JsonValueKind.True;
        string? hoursJson = root.TryGetProperty("regularOpeningHours", out var hours) ? hours.GetRawText() : null;
        string? currentHours = root.TryGetProperty("currentOpeningHours", out var ch) ? ch.GetRawText() : null;
        string? editorial = null;
        if (root.TryGetProperty("editorialSummary", out var ed))
            editorial = Str(ed, "text");
        string? plus = null;
        if (root.TryGetProperty("plusCode", out var pc))
            plus = FirstNonEmpty(Str(pc, "globalCode"), Str(pc, "compoundCode"));
        return new PlaceDetailsRich(
            Str(root, "id").Length > 0 ? Str(root, "id") : placeId,
            DisplayName(root),
            root.TryGetProperty("rating", out var rating) ? rating.GetDecimal() : null,
            root.TryGetProperty("userRatingCount", out var count) ? count.GetInt32() : null,
            Str(root, "googleMapsUri"),
            hoursJson,
            currentHours,
            openNow,
            Coord(root, "latitude"),
            Coord(root, "longitude"),
            Str(root, "formattedAddress"),
            Str(root, "websiteUri"),
            Str(root, "nationalPhoneNumber"),
            Str(root, "internationalPhoneNumber"),
            Str(root, "businessStatus"),
            editorial,
            JoinTypes(root),
            Str(root, "primaryType"),
            Str(root, "priceLevel"),
            plus,
            AttributesJson(root),
            photos,
            reviews);
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

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static List<PlacePhotoRef> ParsePhotos(JsonElement root)
    {
        var photos = new List<PlacePhotoRef>();
        if (!root.TryGetProperty("photos", out var photoArr))
            return photos;
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
        return photos;
    }

    private static List<PlaceReviewRef> ParseReviews(JsonElement root)
    {
        var reviews = new List<PlaceReviewRef>();
        if (!root.TryGetProperty("reviews", out var arr))
            return reviews;
        foreach (var r in arr.EnumerateArray().Take(5))
        {
            string? text = null;
            if (r.TryGetProperty("text", out var t))
                text = Str(t, "text");
            string? author = null, authorUri = null;
            if (r.TryGetProperty("authorAttribution", out var attr))
            {
                author = Str(attr, "displayName");
                authorUri = Str(attr, "uri");
            }
            DateTime? published = null;
            if (r.TryGetProperty("publishTime", out var pt) && pt.ValueKind == JsonValueKind.String
                && DateTime.TryParse(pt.GetString(), out var parsed))
                published = parsed.ToUniversalTime();
            reviews.Add(new PlaceReviewRef(
                author,
                authorUri,
                r.TryGetProperty("rating", out var rating) ? rating.GetDecimal() : null,
                text,
                Str(r, "relativePublishTimeDescription"),
                published,
                FirstNonEmpty(Str(r, "googleMapsUri"), authorUri ?? "")));
        }
        return reviews;
    }

    private static string? JoinTypes(JsonElement root)
    {
        if (!root.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Array)
            return null;
        var list = types.EnumerateArray()
            .Where(t => t.ValueKind == JsonValueKind.String)
            .Select(t => t.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(12)
            .ToList();
        return list.Count == 0 ? null : string.Join(',', list);
    }

    private static string? AttributesJson(JsonElement root)
    {
        var parts = new Dictionary<string, JsonElement>();
        foreach (var name in new[] { "parkingOptions", "paymentOptions", "accessibilityOptions" })
        {
            if (root.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                parts[name] = el.Clone();
        }
        if (parts.Count == 0)
            return null;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var kv in parts)
            {
                writer.WritePropertyName(kv.Key);
                kv.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
