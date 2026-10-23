using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Data;

namespace Ntca.Web.Services;

public sealed record GooglePlacesPlan(int TextSearch, int PlaceDetails, int PlacePhotos, int Shops)
{
    public int Total => TextSearch + PlaceDetails + PlacePhotos;
}

public sealed record GooglePlacesRunResult(
    bool DryRun,
    bool NoOp,
    string Message,
    GooglePlacesPlan Planned,
    int Matched,
    int PendingReview,
    int Refreshed,
    int PhotosSaved,
    int Purged,
    int CallsUsed,
    bool HitLimit);

public static class GooglePlacesMatchScore
{
    public const decimal AutoApplyThreshold = 0.80m;

    private static readonly Regex NonWord = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "llc", "inc", "the", "and", "cards", "card", "shop", "hobby", "hobbies",
        "collectibles", "collectible", "trading", "sports", "sport"
    };

    public static decimal Score(Shop shop, PlacesSearchHit hit)
    {
        var name = NameScore(shop.Name, hit.DisplayName);
        var addr = (hit.FormattedAddress ?? "").ToLowerInvariant();
        var city = !string.IsNullOrWhiteSpace(shop.City) && addr.Contains(shop.City.Trim().ToLowerInvariant()) ? 1m : 0m;
        var state = StateHit(shop.StateCode, addr);
        var geo = GeoScore(shop, hit);
        var raw = 0.55m * name + 0.20m * city + 0.15m * state + 0.10m * geo;
        return Math.Clamp(Math.Round(raw, 4), 0, 1);
    }

    public static bool AutoApply(decimal confidence, Shop shop, PlacesSearchHit hit)
        => confidence >= AutoApplyThreshold && NameScore(shop.Name, hit.DisplayName) >= 0.45m;

    public static decimal NameScore(string left, string right)
    {
        var a = Tokens(left);
        var b = Tokens(right);
        if (a.Count == 0 || b.Count == 0)
            return 0;
        var inter = a.Intersect(b, StringComparer.Ordinal).Count();
        var union = a.Union(b, StringComparer.Ordinal).Count();
        var jaccard = union == 0 ? 0m : (decimal)inter / union;
        var contains = a.All(t => b.Contains(t)) || b.All(t => a.Contains(t)) ? 0.15m : 0m;
        return Math.Clamp(jaccard + contains, 0, 1);
    }

    public static string SearchQuery(Shop shop)
    {
        var parts = new[] { shop.Name, shop.AddressLine1, shop.City, shop.StateCode, shop.PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(" ", parts);
    }

    private static decimal StateHit(string? code, string address)
    {
        if (string.IsNullOrWhiteSpace(code))
            return 0;
        if (address.Contains(code.Trim(), StringComparison.OrdinalIgnoreCase))
            return 1;
        var name = ShopCopy.StateName(code);
        return name.Length > 0 && address.Contains(name, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static decimal GeoScore(Shop shop, PlacesSearchHit hit)
    {
        if (shop.Latitude is null || shop.Longitude is null || hit.Lat is null || hit.Lng is null)
            return 0.5m;
        var miles = ShopCopy.MilesBetween(
            (double)shop.Latitude.Value, (double)shop.Longitude.Value,
            (double)hit.Lat.Value, (double)hit.Lng.Value);
        if (miles < 0.3) return 1m;
        if (miles < 2) return 0.7m;
        if (miles < 10) return 0.3m;
        return 0m;
    }

    private static List<string> Tokens(string raw)
    {
        var norm = NonWord.Replace((raw ?? "").ToLowerInvariant(), " ").Trim();
        return norm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !Noise.Contains(t))
            .ToList();
    }
}

public sealed class GooglePlacesSync
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromDays(30);

    private readonly NtcaDbContext _db;
    private readonly IGooglePlacesClient _client;
    private readonly IFileStorage _files;
    private readonly GooglePlacesOptions _opt;
    private readonly ILogger<GooglePlacesSync> _log;

    public GooglePlacesSync(
        NtcaDbContext db,
        IGooglePlacesClient client,
        IFileStorage files,
        IOptions<GooglePlacesOptions> opt,
        ILogger<GooglePlacesSync> log)
    {
        _db = db;
        _client = client;
        _files = files;
        _opt = opt.Value;
        _log = log;
    }

    public bool IsConfigured => _client.IsConfigured;

    public async Task<GooglePlacesRunResult> MatchAsync(bool dryRun, int? take, CancellationToken ct)
    {
        if (!IsConfigured)
            return NoOp("GooglePlaces:ApiKey is missing or GooglePlaces:Enabled is false. Feature is off.");
        List<Shop> shops;
        try
        {
            shops = await UnmatchedShops(take, ct);
        }
        catch (SqlException ex) when (IsMissingObject(ex))
        {
            return NoOp("Google Places tables are missing. Run db/ssms/05_google_places_SSMS.sql.");
        }
        var plan = new GooglePlacesPlan(shops.Count, 0, 0, shops.Count);
        if (dryRun)
            return new GooglePlacesRunResult(true, false, "Dry-run match plan.", plan, 0, 0, 0, 0, 0, 0, false);

        var matched = 0;
        var pending = 0;
        var used = 0;
        foreach (var shop in shops)
        {
            if (!await TryConsumeAsync(GooglePlacesSkus.TextSearch, ct))
                return Done(false, plan, matched, pending, 0, 0, 0, used, true, "Daily or monthly call limit reached.");
            used++;
            IReadOnlyList<PlacesSearchHit> hits;
            try
            {
                hits = await _client.SearchTextAsync(GooglePlacesMatchScore.SearchQuery(shop), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Text Search failed for shop {ShopId}", shop.ShopId);
                continue;
            }
            var hit = hits.FirstOrDefault();
            var row = await _db.GooglePlaceMatches.FirstOrDefaultAsync(m => m.ShopId == shop.ShopId, ct)
                      ?? new GooglePlaceMatch { ShopId = shop.ShopId, CreatedAt = DateTime.UtcNow };
            if (row.CreatedAt == default)
                row.CreatedAt = DateTime.UtcNow;
            if (hit is null)
            {
                row.MatchStatus = "UNMATCHED";
                row.UpdatedAt = DateTime.UtcNow;
                if (_db.Entry(row).State == EntityState.Detached)
                    _db.GooglePlaceMatches.Add(row);
                await _db.SaveChangesAsync(ct);
                continue;
            }
            var score = GooglePlacesMatchScore.Score(shop, hit);
            row.Confidence = score;
            row.CandidatePlaceId = hit.PlaceId;
            row.CandidateName = Cut(hit.DisplayName, 300);
            row.CandidateAddress = Cut(hit.FormattedAddress, 400);
            row.CandidateLat = hit.Lat;
            row.CandidateLng = hit.Lng;
            row.UpdatedAt = DateTime.UtcNow;
            if (GooglePlacesMatchScore.AutoApply(score, shop, hit))
            {
                ApplyMatch(row, shop, hit.PlaceId, hit.Lat, hit.Lng, "MATCHED");
                matched++;
            }
            else
            {
                row.MatchStatus = "PENDING_REVIEW";
                pending++;
            }
            if (_db.Entry(row).State == EntityState.Detached)
                _db.GooglePlaceMatches.Add(row);
            await _db.SaveChangesAsync(ct);
        }
        return Done(false, plan, matched, pending, 0, 0, 0, used, false, $"Matched {matched}, queued {pending} for review.");
    }

    public async Task<GooglePlacesRunResult> RefreshAsync(bool dryRun, IReadOnlyList<long>? shopIds, int? take, CancellationToken ct)
    {
        if (!IsConfigured)
            return NoOp("GooglePlaces:ApiKey is missing or GooglePlaces:Enabled is false. Feature is off.");
        List<GooglePlaceMatch> targets;
        try
        {
            targets = await RefreshTargets(shopIds, take, ct);
        }
        catch (SqlException ex) when (IsMissingObject(ex))
        {
            return NoOp("Google Places tables are missing. Run db/ssms/05_google_places_SSMS.sql.");
        }

        var photoSlots = Math.Clamp(_opt.PhotosPerShop, 1, 3);
        var details = 0;
        var photos = 0;
        var shops = new List<(GooglePlaceMatch Match, Shop Shop, bool SkipHours, bool SkipPhotos)>();
        foreach (var match in targets)
        {
            var shop = match.Shop ?? await _db.Shops.Include(s => s.Hours).FirstAsync(s => s.ShopId == match.ShopId, ct);
            var skipHours = ShopCopy.HasUsableHours(shop.Hours) || ShopAdminCopy.Locks(shop, "hours");
            var skipPhotos = await HasOwnerPhotosAsync(shop, ct);
            shops.Add((match, shop, skipHours, skipPhotos));
            details++;
            if (!skipPhotos)
                photos += photoSlots;
        }
        var plan = new GooglePlacesPlan(0, details, photos, shops.Count);
        if (dryRun)
            return new GooglePlacesRunResult(true, false, "Dry-run refresh plan.", plan, 0, 0, 0, 0, 0, 0, false);

        var refreshed = 0;
        var photosSaved = 0;
        var used = 0;
        foreach (var item in shops)
        {
            if (!await TryConsumeAsync(GooglePlacesSkus.PlaceDetails, ct))
            {
                var stopped = await PurgeExpiredAsync(ct);
                return Done(false, plan, 0, 0, refreshed, photosSaved, stopped, used, true, "Daily or monthly call limit reached.");
            }
            used++;
            PlaceDetailsResult? detailsResult;
            try
            {
                detailsResult = await _client.GetDetailsAsync(
                    item.Match.GooglePlaceId!,
                    new PlacesDetailFields(!item.SkipHours, !item.SkipPhotos),
                    ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Place Details failed for shop {ShopId}", item.Shop.ShopId);
                await ExpireIfStaleAsync(item.Shop.ShopId, ct);
                continue;
            }
            if (detailsResult is null)
            {
                await ExpireIfStaleAsync(item.Shop.ShopId, ct);
                continue;
            }
            await UpsertCacheAsync(item.Shop, detailsResult, item.SkipHours, ct);
            if (detailsResult.Lat is { } la && detailsResult.Lng is { } ln)
            {
                item.Shop.Latitude = la;
                item.Shop.Longitude = ln;
                item.Shop.UpdatedAt = DateTime.UtcNow;
            }
            refreshed++;
            if (!item.SkipPhotos)
            {
                var saved = await SavePhotosAsync(item.Shop, detailsResult, photoSlots, ct);
                photosSaved += saved;
                used += saved;
            }
            await _db.SaveChangesAsync(ct);
        }
        var purged = await PurgeExpiredAsync(ct);
        return Done(false, plan, 0, 0, refreshed, photosSaved, purged, used, false,
            $"Refreshed {refreshed} shops, saved {photosSaved} photos, purged {purged} expired.");
    }

    public async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var photos = await _db.GooglePlacePhotos.Where(p => p.ExpiresUtc < now).ToListAsync(ct);
        foreach (var photo in photos)
        {
            await _files.DeleteAsync(photo.RelativePath, ct);
            _db.GooglePlacePhotos.Remove(photo);
        }
        var stale = await _db.GooglePlaceCaches.Where(c => c.ExpiresUtc != null && c.ExpiresUtc < now).ToListAsync(ct);
        foreach (var cache in stale)
        {
            cache.Rating = null;
            cache.UserRatingCount = null;
            cache.HoursJson = null;
            cache.OpenNow = null;
            cache.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);
        return photos.Count + stale.Count;
    }

    public async Task<IReadOnlyDictionary<long, GooglePlaceView>> LoadViewsAsync(IEnumerable<long> shopIds, CancellationToken ct)
    {
        var ids = shopIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<long, GooglePlaceView>();
        try
        {
            var now = DateTime.UtcNow;
            var caches = await _db.GooglePlaceCaches.AsNoTracking()
                .Where(c => ids.Contains(c.ShopId) && (c.ExpiresUtc == null || c.ExpiresUtc > now))
                .ToListAsync(ct);
            var photos = await _db.GooglePlacePhotos.AsNoTracking()
                .Where(p => ids.Contains(p.ShopId) && p.ExpiresUtc > now)
                .OrderBy(p => p.Slot)
                .ToListAsync(ct);
            return caches.ToDictionary(
                c => c.ShopId,
                c => new GooglePlaceView(
                    c,
                    photos.Where(p => p.ShopId == c.ShopId).ToList()));
        }
        catch (SqlException ex) when (IsMissingObject(ex))
        {
            return new Dictionary<long, GooglePlaceView>();
        }
    }

    public async Task<(int Unmatched, int Pending, int Matched, int Rejected, DateTime? LastRefresh)> StatusAsync(CancellationToken ct)
    {
        try
        {
            var groups = await _db.GooglePlaceMatches.AsNoTracking()
                .GroupBy(m => m.MatchStatus)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct);
            int Count(string s) => groups.FirstOrDefault(g => g.Key == s)?.Count ?? 0;
            var last = await _db.GooglePlaceCaches.AsNoTracking()
                .MaxAsync(c => (DateTime?)c.RefreshedUtc, ct);
            return (Count("UNMATCHED"), Count("PENDING_REVIEW"), Count("MATCHED") + Count("MANUAL"), Count("REJECTED"), last);
        }
        catch (SqlException ex) when (IsMissingObject(ex))
        {
            return (0, 0, 0, 0, null);
        }
    }

    public async Task<IReadOnlyList<(DateTime Day, string Sku, int Count)>> CallLogAsync(int days, CancellationToken ct)
    {
        try
        {
            var since = DateTime.UtcNow.Date.AddDays(-days);
            return await _db.GoogleApiCallLogs.AsNoTracking()
                .Where(l => l.CallDate >= since)
                .OrderByDescending(l => l.CallDate)
                .ThenBy(l => l.Sku)
                .Select(l => new ValueTuple<DateTime, string, int>(l.CallDate, l.Sku, l.CallCount))
                .ToListAsync(ct);
        }
        catch (SqlException ex) when (IsMissingObject(ex))
        {
            return [];
        }
    }

    public async Task<(int Today, int Month)> UsageAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        try
        {
            var rows = await _db.GoogleApiCallLogs.AsNoTracking()
                .Where(l => l.CallDate >= monthStart)
                .ToListAsync(ct);
            return (rows.Where(r => r.CallDate == today).Sum(r => r.CallCount), rows.Sum(r => r.CallCount));
        }
        catch (SqlException ex) when (IsMissingObject(ex))
        {
            return (0, 0);
        }
    }

    public async Task ApplyReviewAsync(long shopId, string decision, string? placeId, long? userId, CancellationToken ct)
    {
        var row = await _db.GooglePlaceMatches.FirstOrDefaultAsync(m => m.ShopId == shopId, ct);
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.ShopId == shopId, ct);
        if (row is null || shop is null)
            return;
        row.ReviewedAt = DateTime.UtcNow;
        row.ReviewedByUserId = userId;
        row.UpdatedAt = DateTime.UtcNow;
        if (decision == "reject")
        {
            row.MatchStatus = "REJECTED";
            row.GooglePlaceId = null;
        }
        else
        {
            var id = string.IsNullOrWhiteSpace(placeId) ? row.CandidatePlaceId : placeId.Trim();
            if (string.IsNullOrWhiteSpace(id))
                return;
            ApplyMatch(row, shop, id, row.CandidateLat, row.CandidateLng, decision == "manual" ? "MANUAL" : "MATCHED");
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<Shop>> UnmatchedShops(int? take, CancellationToken ct)
    {
        var matchedIds = _db.GooglePlaceMatches
            .Where(m => m.MatchStatus == "MATCHED" || m.MatchStatus == "MANUAL" || m.MatchStatus == "PENDING_REVIEW" || m.MatchStatus == "REJECTED")
            .Select(m => m.ShopId);
        var q = _db.Shops.AsNoTracking()
            .Where(s => s.ListingStatus == "ACTIVE" && !matchedIds.Contains(s.ShopId))
            .OrderBy(s => s.ShopId);
        if (take is > 0)
            return await q.Take(take.Value).ToListAsync(ct);
        return await q.ToListAsync(ct);
    }

    private async Task<List<GooglePlaceMatch>> RefreshTargets(IReadOnlyList<long>? shopIds, int? take, CancellationToken ct)
    {
        var q = _db.GooglePlaceMatches
            .Include(m => m.Shop)
                .ThenInclude(s => s!.Hours)
            .Where(m => (m.MatchStatus == "MATCHED" || m.MatchStatus == "MANUAL") && m.GooglePlaceId != null);
        if (shopIds is { Count: > 0 })
            q = q.Where(m => shopIds.Contains(m.ShopId));
        var list = await q.ToListAsync(ct);
        var caches = await _db.GooglePlaceCaches.AsNoTracking()
            .Where(c => list.Select(m => m.ShopId).Contains(c.ShopId))
            .ToListAsync(ct);
        var byShop = caches.ToDictionary(c => c.ShopId);
        var ordered = list
            .OrderBy(m => byShop.TryGetValue(m.ShopId, out var c) ? c.RefreshedUtc ?? DateTime.MinValue : DateTime.MinValue)
            .ThenBy(m => m.ShopId)
            .ToList();
        var n = take ?? NightlyTake(ordered.Count);
        return ordered.Take(n).ToList();
    }

    public int NightlyTake(int matchedCount)
    {
        if (_opt.NightlyRefreshBatch > 0)
            return _opt.NightlyRefreshBatch;
        return Math.Max(1, (int)Math.Ceiling(matchedCount / 30.0));
    }

    private async Task UpsertCacheAsync(Shop shop, PlaceDetailsResult details, bool skipHours, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cache = await _db.GooglePlaceCaches.FirstOrDefaultAsync(c => c.ShopId == shop.ShopId, ct);
        if (cache is null)
        {
            cache = new GooglePlaceCache { ShopId = shop.ShopId, CreatedAt = now };
            _db.GooglePlaceCaches.Add(cache);
        }
        cache.GooglePlaceId = details.PlaceId;
        cache.Rating = details.Rating;
        cache.UserRatingCount = details.UserRatingCount;
        cache.GoogleMapsUri = Cut(details.GoogleMapsUri, 500);
        if (!skipHours)
        {
            cache.HoursJson = details.HoursJson;
            cache.OpenNow = details.OpenNow;
        }
        cache.DisplayName = Cut(details.DisplayName, 300);
        cache.Latitude = details.Lat;
        cache.Longitude = details.Lng;
        cache.RefreshedUtc = now;
        cache.ExpiresUtc = now.Add(CacheTtl);
        cache.UpdatedAt = now;
    }

    private async Task<int> SavePhotosAsync(Shop shop, PlaceDetailsResult details, int slots, CancellationToken ct)
    {
        var existing = await _db.GooglePlacePhotos.Where(p => p.ShopId == shop.ShopId).ToListAsync(ct);
        foreach (var old in existing)
        {
            await _files.DeleteAsync(old.RelativePath, ct);
            _db.GooglePlacePhotos.Remove(old);
        }
        var saved = 0;
        var now = DateTime.UtcNow;
        foreach (var photo in details.Photos.Take(slots))
        {
            if (!await TryConsumeAsync(GooglePlacesSkus.PlacePhoto, ct))
                break;
            byte[]? bytes;
            try
            {
                bytes = await _client.GetPhotoAsync(photo.Name, _opt.PhotoMaxWidthPx, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Place Photo failed for shop {ShopId}", shop.ShopId);
                continue;
            }
            if (bytes is null || bytes.Length == 0)
                continue;
            var slot = (byte)(saved + 1);
            var relative = $"gplace/{shop.ShopId}/{slot}.jpg";
            await using var stream = new MemoryStream(bytes);
            await _files.SaveAsync(relative, stream, "image/jpeg", ct);
            _db.GooglePlacePhotos.Add(new GooglePlacePhoto
            {
                ShopId = shop.ShopId,
                GooglePlaceId = details.PlaceId,
                Slot = slot,
                PhotoName = Cut(photo.Name, 400)!,
                AuthorName = Cut(photo.AuthorName, 200),
                AuthorUri = Cut(photo.AuthorUri, 400),
                RelativePath = relative,
                ContentType = "image/jpeg",
                FetchedUtc = now,
                ExpiresUtc = now.Add(CacheTtl),
                CreatedAt = now
            });
            saved++;
        }
        return saved;
    }

    private async Task ExpireIfStaleAsync(long shopId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cache = await _db.GooglePlaceCaches.FirstOrDefaultAsync(c => c.ShopId == shopId, ct);
        if (cache?.ExpiresUtc is { } exp && exp < now)
        {
            cache.Rating = null;
            cache.UserRatingCount = null;
            cache.HoursJson = null;
            cache.OpenNow = null;
            cache.UpdatedAt = now;
        }
        var photos = await _db.GooglePlacePhotos.Where(p => p.ShopId == shopId && p.ExpiresUtc < now).ToListAsync(ct);
        foreach (var photo in photos)
        {
            await _files.DeleteAsync(photo.RelativePath, ct);
            _db.GooglePlacePhotos.Remove(photo);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> HasOwnerPhotosAsync(Shop shop, CancellationToken ct)
    {
        if (ShopAdminCopy.Locks(shop, "photos") || !string.IsNullOrWhiteSpace(shop.LogoObjectKey))
            return true;
        if (await _db.ShopPhotos.AnyAsync(p => p.ShopId == shop.ShopId && p.ModerationStatus == "APPROVED", ct))
            return true;
        return await _files.ExistsAsync($"shops/{shop.ShopId}/1.jpg", ct)
            || await _files.ExistsAsync($"shops/{shop.ShopId}/1.png", ct);
    }

    private static void ApplyMatch(GooglePlaceMatch row, Shop shop, string placeId, decimal? lat, decimal? lng, string status)
    {
        row.GooglePlaceId = placeId;
        row.MatchStatus = status;
        row.MatchedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        if (lat is { } la && lng is { } ln && !ShopAdminCopy.Locks(shop, "location"))
        {
            shop.Latitude = la;
            shop.Longitude = ln;
            shop.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<bool> TryConsumeAsync(string sku, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var rows = await _db.GoogleApiCallLogs.Where(l => l.CallDate >= monthStart).ToListAsync(ct);
        var day = rows.Where(r => r.CallDate == today).Sum(r => r.CallCount);
        var month = rows.Sum(r => r.CallCount);
        if (day >= _opt.DailyCallLimit || month >= _opt.MonthlyCallLimit)
            return false;
        var log = rows.FirstOrDefault(r => r.CallDate == today && r.Sku == sku);
        if (log is null)
        {
            log = new GoogleApiCallLog { CallDate = today, Sku = sku, CallCount = 0, UpdatedAt = DateTime.UtcNow };
            _db.GoogleApiCallLogs.Add(log);
        }
        log.CallCount++;
        log.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static GooglePlacesRunResult NoOp(string message)
        => new(false, true, message, new GooglePlacesPlan(0, 0, 0, 0), 0, 0, 0, 0, 0, 0, false);

    private static GooglePlacesRunResult Done(
        bool dry, GooglePlacesPlan plan, int matched, int pending, int refreshed, int photos, int purged, int used, bool limit, string message)
        => new(dry, false, message, plan, matched, pending, refreshed, photos, purged, used, limit);

    private static bool IsMissingObject(SqlException ex)
        => ex.Number is 208 or 207;

    private static string? Cut(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
}

public sealed record GooglePlaceView(GooglePlaceCache Cache, IReadOnlyList<GooglePlacePhoto> Photos)
{
    public bool IsFresh => Cache.ExpiresUtc is null || Cache.ExpiresUtc > DateTime.UtcNow;
    public string PhotoUrl(byte slot) => "/media/gplace/" + Cache.ShopId + "/" + slot;
}
