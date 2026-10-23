using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Data;

namespace Ntca.Web.Services;

public sealed record PlacesEnrichPlan(
    int ShopsTotal,
    int WithPlaceId,
    int NeedMatch,
    int NeedDetails,
    int NeedWebsite,
    int TextSearch,
    int PlaceDetails,
    int PlacePhotos)
{
    public int TotalCalls => TextSearch + PlaceDetails + PlacePhotos;
}

public sealed record PlacesEnrichResult(
    bool DryRun,
    bool NoOp,
    string Message,
    PlacesEnrichPlan Plan,
    int Matched,
    int PendingReview,
    int DetailsSaved,
    int PhotosSaved,
    int ReviewsSaved,
    int WebsiteFilled,
    int CallsUsed,
    bool HitLimit,
    IReadOnlyList<PlacesRankedShop> Ranked)
{
    public PlacesEnrichResult(
        bool dryRun, bool noOp, string message, PlacesEnrichPlan plan,
        int matched, int pendingReview, int detailsSaved, int photosSaved, int reviewsSaved,
        int websiteFilled, int callsUsed, bool hitLimit)
        : this(dryRun, noOp, message, plan, matched, pendingReview, detailsSaved, photosSaved,
            reviewsSaved, websiteFilled, callsUsed, hitLimit, []) { }
}

public sealed class PlacesEnrich
{
    public static readonly TimeSpan FreshTtl = TimeSpan.FromDays(30);
    public const int DefaultRateMs = 1500;
    public const int DefaultMatchRadiusMeters = 16093;
    public const int EnrichPhotoSlots = 5;

    private readonly NtcaDbContext _db;
    private readonly IGooglePlacesClient _client;
    private readonly GooglePlacesSync _sync;
    private readonly WebsiteEnrich _web;
    private readonly GooglePlacesOptions _opt;
    private readonly ILogger<PlacesEnrich> _log;

    public PlacesEnrich(
        NtcaDbContext db,
        IGooglePlacesClient client,
        GooglePlacesSync sync,
        WebsiteEnrich web,
        IOptions<GooglePlacesOptions> opt,
        ILogger<PlacesEnrich> log)
    {
        _db = db;
        _client = client;
        _sync = sync;
        _web = web;
        _opt = opt.Value;
        _log = log;
    }

    public bool IsConfigured => _client.IsConfigured;

    public async Task<PlacesEnrichResult> EnrichAsync(
        bool dryRun,
        int? limit,
        int? maxCalls,
        int rateMs,
        bool skipWebsite,
        long? shopId,
        bool force,
        CancellationToken ct,
        int? top = null,
        string? citiesPath = null,
        string? exportPath = null)
    {
        if (!IsConfigured)
            return NoOp("GooglePlaces:ApiKey is missing or GooglePlaces:Enabled is false. Feature is off.");

        List<Shop> shops;
        try
        {
            shops = await ActiveShops(shopId, ct);
        }
        catch (SqlException ex) when (ex.Number is 208 or 207)
        {
            return NoOp("Places tables are missing. Run db/ssms/05_google_places_SSMS.sql then db/ssms/09_places_enrich_SSMS.sql.");
        }

        Dictionary<long, GooglePlaceMatch> matches;
        Dictionary<long, ShopPlaceEnrich> enrichRows;
        Dictionary<long, GooglePlaceCache> caches;
        try
        {
            matches = await _db.GooglePlaceMatches.Where(m => shops.Select(s => s.ShopId).Contains(m.ShopId))
                .ToDictionaryAsync(m => m.ShopId, ct);
            enrichRows = await _db.ShopPlaceEnrichments.Where(e => shops.Select(s => s.ShopId).Contains(e.ShopId))
                .ToDictionaryAsync(e => e.ShopId, ct);
            caches = await _db.GooglePlaceCaches.Where(c => shops.Select(s => s.ShopId).Contains(c.ShopId))
                .ToDictionaryAsync(c => c.ShopId, ct);
        }
        catch (SqlException ex) when (ex.Number is 208 or 207)
        {
            return NoOp("Places enrich tables are missing. Run db/ssms/09_places_enrich_SSMS.sql.");
        }

        var now = DateTime.UtcNow;
        HashSet<long> ownerIds;
        try
        {
            ownerIds = (await _db.ShopMembers.AsNoTracking()
                .Where(m => m.RevokedAt == null && m.MemberRole == "OWNER")
                .Select(m => m.ShopId)
                .ToListAsync(ct)).ToHashSet();
        }
        catch (SqlException ex) when (ex.Number is 208 or 207)
        {
            ownerIds = [];
        }

        var cities = UsCitiesLookup.Load(citiesPath);
        (bool Enriched, bool NeedsMatch, bool NeedsDetails, bool NeedsWebsite) StatusOf(Shop shop)
        {
            matches.TryGetValue(shop.ShopId, out var match);
            enrichRows.TryGetValue(shop.ShopId, out var enrich);
            caches.TryGetValue(shop.ShopId, out var cache);
            var linked = match is { GooglePlaceId: not null }
                         && (match.MatchStatus is "MATCHED" or "MANUAL");
            var needsMatch = !linked && (match is null || match.MatchStatus is "UNMATCHED" || force);
            var detailsFresh = linked && !force
                && (enrich?.LastDetailsUtc is { } d && now - d < FreshTtl
                    || cache?.ExpiresUtc is { } exp && exp > now && !string.IsNullOrWhiteSpace(cache.WebsiteUri));
            var needsDetails = linked && !detailsFresh;
            var webFresh = !force && enrich?.LastWebsiteUtc is { } w && now - w < FreshTtl;
            var needsWebsite = !skipWebsite && !webFresh;
            if (!linked)
                needsWebsite = false;
            var needsWork = needsMatch || needsDetails || needsWebsite;
            return (!needsWork, needsMatch, needsDetails, needsWebsite);
        }

        IReadOnlyList<PlacesRankedShop> ranked = [];
        if (top is > 0 || !string.IsNullOrWhiteSpace(exportPath))
        {
            ranked = PlacesPriority.TakeTop(
                PlacesPriority.Rank(shops, ownerIds, cities, StatusOf),
                top);
            shops = ranked.Select(r => r.Shop).ToList();
        }

        var needMatch = new List<Shop>();
        var needDetails = new List<(Shop Shop, GooglePlaceMatch Match)>();
        var needWebsite = new List<Shop>();
        foreach (var shop in shops)
        {
            var st = StatusOf(shop);
            if (st.NeedsMatch)
                needMatch.Add(shop);
            if (st.NeedsDetails && matches.TryGetValue(shop.ShopId, out var match))
                needDetails.Add((shop, match));
            if (st.NeedsWebsite)
                needWebsite.Add(shop);
        }

        if (limit is > 0)
        {
            var workIds = PlacesPriority.TakeWork(
                    ranked.Count > 0
                        ? ranked
                        : shops.Select(s =>
                        {
                            var st = StatusOf(s);
                            return new PlacesRankedShop(0, s, PlacesPriorityParts.From(s, ownerIds.Contains(s.ShopId), 0),
                                st.Enriched, st.NeedsMatch, st.NeedsDetails, st.NeedsWebsite);
                        }).ToList(),
                    limit)
                .Select(r => r.ShopId)
                .ToHashSet();
            needMatch = needMatch.Where(s => workIds.Contains(s.ShopId)).ToList();
            needDetails = needDetails.Where(x => workIds.Contains(x.Shop.ShopId)).ToList();
            needWebsite = needWebsite.Where(s => workIds.Contains(s.ShopId)).ToList();
        }

        var photoSlots = Math.Clamp(Math.Max(_opt.PhotosPerShop, EnrichPhotoSlots), 1, 8);
        var plan = new PlacesEnrichPlan(
            shops.Count,
            matches.Values.Count(m => m.GooglePlaceId != null && m.MatchStatus is "MATCHED" or "MANUAL"),
            needMatch.Count,
            needDetails.Count,
            needWebsite.Count,
            needMatch.Count,
            needDetails.Count,
            needDetails.Count * photoSlots);

        if (!string.IsNullOrWhiteSpace(exportPath) && ranked.Count > 0)
            PlacesPriority.WriteCsv(exportPath, ranked);

        if (dryRun)
        {
            var message = FormatDryRun(plan);
            if (ranked.Count > 0)
                message = PlacesPriority.FormatList(ranked) + Environment.NewLine + message;
            if (cities.Count == 0 && (top is > 0 || !string.IsNullOrWhiteSpace(exportPath)))
                message += " City population file missing — pop=0 for every shop. Expected data/places/uscities.tsv.";
            return new PlacesEnrichResult(true, false, message, plan, 0, 0, 0, 0, 0, 0, 0, false, ranked);
        }

        var used = 0;
        var matched = 0;
        var pending = 0;
        var detailsSaved = 0;
        var photosSaved = 0;
        var reviewsSaved = 0;
        var websiteFilled = 0;
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, rateMs));

        foreach (var shop in needMatch)
        {
            if (HitCap(used, maxCalls) || !await _sync.TryConsumeCallAsync(GooglePlacesSkus.TextSearch, ct))
                return Done(plan, matched, pending, detailsSaved, photosSaved, reviewsSaved, websiteFilled, used, true);
            used++;
            IReadOnlyList<PlacesSearchHit> hits;
            try
            {
                hits = await _client.SearchTextNearAsync(
                    GooglePlacesMatchScore.SearchQuery(shop),
                    shop.Latitude,
                    shop.Longitude,
                    DefaultMatchRadiusMeters,
                    ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Match search failed for shop {ShopId}", shop.ShopId);
                await TouchEnrichAsync(shop.ShopId, match: true, error: Cut(ex.Message, 400), ct: ct);
                await DelayAsync(delay, ct);
                continue;
            }
            var hit = hits.FirstOrDefault();
            var row = matches.GetValueOrDefault(shop.ShopId)
                      ?? new GooglePlaceMatch { ShopId = shop.ShopId, CreatedAt = DateTime.UtcNow };
            if (_db.Entry(row).State == EntityState.Detached && !matches.ContainsKey(shop.ShopId))
                _db.GooglePlaceMatches.Add(row);
            row.UpdatedAt = DateTime.UtcNow;
            if (hit is null)
            {
                row.MatchStatus = "UNMATCHED";
            }
            else
            {
                var score = GooglePlacesMatchScore.Score(shop, hit);
                row.Confidence = score;
                row.CandidatePlaceId = hit.PlaceId;
                row.CandidateName = Cut(hit.DisplayName, 300);
                row.CandidateAddress = Cut(hit.FormattedAddress, 400);
                row.CandidateLat = hit.Lat;
                row.CandidateLng = hit.Lng;
                if (GooglePlacesMatchScore.AutoApply(score, shop, hit))
                {
                    row.GooglePlaceId = hit.PlaceId;
                    row.MatchStatus = "MATCHED";
                    row.MatchedAt = DateTime.UtcNow;
                    if (hit.Lat is { } la && hit.Lng is { } ln && !ShopAdminCopy.Locks(shop, "location")
                        && shop.Latitude is null)
                    {
                        shop.Latitude = la;
                        shop.Longitude = ln;
                        shop.UpdatedAt = DateTime.UtcNow;
                    }
                    matched++;
                    needDetails.Add((shop, row));
                }
                else
                {
                    row.MatchStatus = "PENDING_REVIEW";
                    pending++;
                }
            }
            await TouchEnrichAsync(shop.ShopId, match: true, ct: ct);
            await _db.SaveChangesAsync(ct);
            await DelayAsync(delay, ct);
        }

        foreach (var item in needDetails)
        {
            if (HitCap(used, maxCalls) || !await _sync.TryConsumeCallAsync(GooglePlacesSkus.PlaceDetails, ct))
                return Done(plan, matched, pending, detailsSaved, photosSaved, reviewsSaved, websiteFilled, used, true);
            used++;
            PlaceDetailsRich? details;
            try
            {
                details = await _client.GetDetailsRichAsync(item.Match.GooglePlaceId!, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Rich details failed for shop {ShopId}", item.Shop.ShopId);
                await TouchEnrichAsync(item.Shop.ShopId, details: true, error: Cut(ex.Message, 400), ct: ct);
                await DelayAsync(delay, ct);
                continue;
            }
            if (details is null)
            {
                await TouchEnrichAsync(item.Shop.ShopId, details: true, error: "empty details", ct: ct);
                await DelayAsync(delay, ct);
                continue;
            }
            await UpsertRichCacheAsync(item.Shop, details, ct);
            FillShopBlanks(item.Shop, details);
            var reviewCount = await ReplaceReviewsAsync(item.Shop, details, ct);
            reviewsSaved += reviewCount;
            detailsSaved++;
            var skipPhotos = ShopAdminCopy.Locks(item.Shop, "photos")
                             || !string.IsNullOrWhiteSpace(item.Shop.LogoObjectKey)
                             || await _db.ShopPhotos.AnyAsync(p => p.ShopId == item.Shop.ShopId && p.ModerationStatus == "APPROVED", ct);
            if (!skipPhotos && details.Photos.Count > 0)
            {
                var asCore = new PlaceDetailsResult(
                    details.PlaceId, details.DisplayName, details.Rating, details.UserRatingCount,
                    details.GoogleMapsUri, details.HoursJson, details.OpenNow, details.Lat, details.Lng,
                    details.Photos);
                var saved = await _sync.CachePhotosAsync(item.Shop, asCore, photoSlots, ct);
                photosSaved += saved;
                used += saved;
            }
            await TouchEnrichAsync(item.Shop.ShopId, details: true, ct: ct);
            await _db.SaveChangesAsync(ct);
            await DelayAsync(delay, ct);
        }

        if (!skipWebsite)
        {
            foreach (var shop in needWebsite)
            {
                if (HitCap(used, maxCalls))
                    return Done(plan, matched, pending, detailsSaved, photosSaved, reviewsSaved, websiteFilled, used, true);
                var url = shop.WebsiteUrl
                          ?? caches.GetValueOrDefault(shop.ShopId)?.WebsiteUri
                          ?? (await _db.GooglePlaceCaches.AsNoTracking()
                              .Where(c => c.ShopId == shop.ShopId)
                              .Select(c => c.WebsiteUri)
                              .FirstOrDefaultAsync(ct));
                WebsiteEnrichment found;
                try
                {
                    found = await _web.FetchAsync(url, ct);
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Website enrich failed for shop {ShopId}", shop.ShopId);
                    await TouchEnrichAsync(shop.ShopId, website: true, error: Cut(ex.Message, 400), ct: ct);
                    await _db.SaveChangesAsync(ct);
                    await DelayAsync(delay, ct);
                    continue;
                }
                var filled = await ApplyWebsiteAsync(shop, found, ct);
                websiteFilled += filled;
                await TouchEnrichAsync(shop.ShopId, website: true, emails: string.Join(';', found.Emails.Take(3)), ct: ct);
                await _db.SaveChangesAsync(ct);
                await DelayAsync(delay, ct);
            }
        }

        return Done(plan, matched, pending, detailsSaved, photosSaved, reviewsSaved, websiteFilled, used, false);
    }

    public async Task<PlacesEnrichPlan> StatusPlanAsync(CancellationToken ct)
    {
        var result = await EnrichAsync(true, null, null, 0, false, null, false, ct);
        return result.Plan;
    }

    private async Task<List<Shop>> ActiveShops(long? shopId, CancellationToken ct)
    {
        var q = _db.Shops.Include(s => s.Hours).Include(s => s.Socials)
            .Where(s => s.ListingStatus == "ACTIVE");
        if (shopId is > 0)
            q = q.Where(s => s.ShopId == shopId);
        return await q.OrderBy(s => s.ShopId).ToListAsync(ct);
    }

    private async Task UpsertRichCacheAsync(Shop shop, PlaceDetailsRich details, CancellationToken ct)
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
        if (!ShopCopy.HasUsableHours(shop.Hours) && !ShopAdminCopy.Locks(shop, "hours"))
        {
            cache.HoursJson = details.HoursJson;
            cache.OpenNow = details.OpenNow;
            cache.CurrentHoursJson = details.CurrentHoursJson;
        }
        cache.DisplayName = Cut(details.DisplayName, 300);
        cache.FormattedAddress = Cut(details.FormattedAddress, 400);
        cache.Latitude = details.Lat;
        cache.Longitude = details.Lng;
        cache.WebsiteUri = Cut(details.WebsiteUri, 500);
        cache.NationalPhone = Cut(details.NationalPhone, 40);
        cache.InternationalPhone = Cut(details.InternationalPhone, 40);
        cache.BusinessStatus = Cut(details.BusinessStatus, 40);
        cache.EditorialSummary = Cut(details.EditorialSummary, 1000);
        cache.TypesJson = Cut(details.TypesJson, 1000);
        cache.PrimaryType = Cut(details.PrimaryType, 80);
        cache.PriceLevel = Cut(details.PriceLevel, 40);
        cache.PlusCode = Cut(details.PlusCode, 40);
        cache.AttributesJson = details.AttributesJson;
        cache.RefreshedUtc = now;
        cache.ExpiresUtc = now.Add(FreshTtl);
        cache.UpdatedAt = now;
    }

    private void FillShopBlanks(Shop shop, PlaceDetailsRich details)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(shop.WebsiteUrl) && !ShopAdminCopy.Locks(shop, "website")
            && !string.IsNullOrWhiteSpace(details.WebsiteUri))
        {
            shop.WebsiteUrl = Cut(details.WebsiteUri, 400);
            RecordSource(shop.ShopId, "website", "google", now);
        }
        if (string.IsNullOrWhiteSpace(shop.Phone) && !ShopAdminCopy.Locks(shop, "phone")
            && !string.IsNullOrWhiteSpace(details.NationalPhone))
        {
            shop.Phone = Cut(details.NationalPhone, 40);
            RecordSource(shop.ShopId, "phone", "google", now);
        }
        if (string.IsNullOrWhiteSpace(shop.Description) && !ShopAdminCopy.Locks(shop, "description")
            && !string.IsNullOrWhiteSpace(details.EditorialSummary))
        {
            shop.Description = Cut(details.EditorialSummary, 2000);
            RecordSource(shop.ShopId, "description", "google", now);
        }
        if (shop.Latitude is null && details.Lat is { } la && details.Lng is { } ln
            && !ShopAdminCopy.Locks(shop, "location"))
        {
            shop.Latitude = la;
            shop.Longitude = ln;
            RecordSource(shop.ShopId, "location", "google", now);
        }
        shop.UpdatedAt = now;
    }

    private async Task<int> ReplaceReviewsAsync(Shop shop, PlaceDetailsRich details, CancellationToken ct)
    {
        var existing = await _db.GooglePlaceReviews.Where(r => r.ShopId == shop.ShopId).ToListAsync(ct);
        _db.GooglePlaceReviews.RemoveRange(existing);
        var now = DateTime.UtcNow;
        var slot = (byte)1;
        foreach (var review in details.Reviews.Take(5))
        {
            _db.GooglePlaceReviews.Add(new GooglePlaceReview
            {
                ShopId = shop.ShopId,
                GooglePlaceId = details.PlaceId,
                AuthorName = Cut(review.AuthorName, 200),
                AuthorUri = Cut(review.AuthorUri, 400),
                Rating = review.Rating,
                ReviewText = Cut(review.Text, 2000),
                RelativeTime = Cut(review.RelativeTime, 80),
                PublishTime = review.PublishTime,
                AttributionUri = Cut(review.AttributionUri, 500),
                SortOrder = slot,
                FetchedUtc = now,
                ExpiresUtc = now.Add(FreshTtl),
                CreatedAt = now
            });
            slot++;
        }
        return slot - 1;
    }

    private async Task<int> ApplyWebsiteAsync(Shop shop, WebsiteEnrichment found, CancellationToken ct)
    {
        var filled = 0;
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(shop.PublicEmail) && !ShopAdminCopy.Locks(shop, "email")
            && found.Emails.Count > 0)
        {
            shop.PublicEmail = Cut(found.Emails[0], 200);
            RecordSource(shop.ShopId, "email", "website", now);
            filled++;
        }
        var existing = await _db.ShopSocials.Where(s => s.ShopId == shop.ShopId).ToListAsync(ct);
        foreach (var kv in found.Socials)
        {
            if (existing.Any(s => s.PlatformCode.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)))
                continue;
            if (ShopAdminCopy.Locks(shop, "socials") || ShopAdminCopy.Locks(shop, kv.Key.ToLowerInvariant()))
                continue;
            if (!ShopSocialNormalizer.TryNormalize(kv.Key, kv.Value, out var url, out var handle, out _))
                continue;
            var platform = ShopSocialCatalog.Find(kv.Key);
            _db.ShopSocials.Add(new ShopSocial
            {
                ShopId = shop.ShopId,
                PlatformCode = kv.Key,
                Url = url,
                Handle = handle,
                SortOrder = platform?.DisplayOrder ?? 100,
                Verified = false,
                Source = "website",
                CreatedAt = now,
                UpdatedAt = now
            });
            if (kv.Key == ShopSocialCatalog.Facebook && string.IsNullOrWhiteSpace(shop.FacebookUrl))
                shop.FacebookUrl = url;
            if (kv.Key == ShopSocialCatalog.Instagram && string.IsNullOrWhiteSpace(shop.InstagramHandle))
                shop.InstagramHandle = handle ?? url;
            if (kv.Key == ShopSocialCatalog.Website && string.IsNullOrWhiteSpace(shop.WebsiteUrl))
                shop.WebsiteUrl = url;
            RecordSource(shop.ShopId, kv.Key.ToLowerInvariant(), "website", now);
            filled++;
        }
        shop.UpdatedAt = now;
        return filled;
    }

    private void RecordSource(long shopId, string field, string source, DateTime now)
    {
        var row = _db.ShopFieldSources.Local.FirstOrDefault(s => s.ShopId == shopId && s.FieldName == field);
        if (row is null)
        {
            row = _db.ShopFieldSources.FirstOrDefault(s => s.ShopId == shopId && s.FieldName == field);
            if (row is null)
            {
                row = new ShopFieldSource { ShopId = shopId, FieldName = field };
                _db.ShopFieldSources.Add(row);
            }
        }
        row.Source = source;
        row.FetchedUtc = now;
    }

    private async Task TouchEnrichAsync(
        long shopId, bool match = false, bool details = false, bool website = false,
        string? emails = null, string? error = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var row = await _db.ShopPlaceEnrichments.FirstOrDefaultAsync(e => e.ShopId == shopId, ct);
        if (row is null)
        {
            row = new ShopPlaceEnrich { ShopId = shopId, CreatedAt = now };
            _db.ShopPlaceEnrichments.Add(row);
        }
        if (match) row.LastMatchUtc = now;
        if (details) row.LastDetailsUtc = now;
        if (website) row.LastWebsiteUtc = now;
        if (emails is not null) row.WebsiteEmails = Cut(emails, 500);
        row.LastError = error;
        row.UpdatedAt = now;
    }

    private static bool HitCap(int used, int? maxCalls)
        => maxCalls is > 0 && used >= maxCalls.Value;

    private static async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct);
    }

    private static PlacesEnrichResult NoOp(string message)
        => new(false, true, message, new PlacesEnrichPlan(0, 0, 0, 0, 0, 0, 0, 0), 0, 0, 0, 0, 0, 0, 0, false);

    private static PlacesEnrichResult Done(
        PlacesEnrichPlan plan, int matched, int pending, int details, int photos, int reviews,
        int website, int used, bool limit)
        => new(false, false,
            limit
                ? $"Stopped at call cap or daily/monthly limit after {used} Google calls. Matched {matched}, review {pending}, details {details}, photos {photos}, reviews {reviews}, website fills {website}."
                : $"Enriched: matched {matched}, queued {pending} for review, details {details}, photos {photos}, reviews {reviews}, website fills {website}. Used {used} Google calls.",
            plan, matched, pending, details, photos, reviews, website, used, limit);

    public static string FormatDryRun(PlacesEnrichPlan plan)
    {
        var list = PlacesPricing.ListPrice(plan.TextSearch, PlacesPricing.TextSearchEnterprisePerThousand)
                   + PlacesPricing.ListPrice(plan.PlaceDetails, PlacesPricing.PlaceDetailsAtmospherePerThousand)
                   + PlacesPricing.ListPrice(plan.PlacePhotos, PlacesPricing.PlacePhotoPerThousand);
        var after = PlacesPricing.AfterCap(plan.TextSearch, PlacesPricing.TextSearchEnterpriseFree, PlacesPricing.TextSearchEnterprisePerThousand)
                    + PlacesPricing.AfterCap(plan.PlaceDetails, PlacesPricing.PlaceDetailsAtmosphereFree, PlacesPricing.PlaceDetailsAtmospherePerThousand)
                    + PlacesPricing.AfterCap(plan.PlacePhotos, PlacesPricing.PlacePhotoFree, PlacesPricing.PlacePhotoPerThousand);
        return
            $"Dry-run enrich. shops={plan.ShopsTotal} withPlaceId={plan.WithPlaceId} needMatch={plan.NeedMatch} needDetails={plan.NeedDetails} needWebsite={plan.NeedWebsite}. "
            + $"estCalls textSearch={plan.TextSearch} details={plan.PlaceDetails} photos={plan.PlacePhotos} total={plan.TotalCalls}. "
            + $"SKU: match={PlacesPricing.MatchSku} (${PlacesPricing.TextSearchEnterprisePerThousand}/1000), "
            + $"details={PlacesPricing.DetailsSku} (${PlacesPricing.PlaceDetailsAtmospherePerThousand}/1000), "
            + $"photos={PlacesPricing.PhotoSku} (${PlacesPricing.PlacePhotoPerThousand}/1000). "
            + $"List {PlacesPricing.FormatUsd(list)}; after monthly free caps {PlacesPricing.FormatUsd(after)}.";
    }

    private static string? Cut(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
}
