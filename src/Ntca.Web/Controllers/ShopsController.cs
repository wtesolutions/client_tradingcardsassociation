using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class ShopsController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IImpressionTracker _track;
    private readonly GooglePlacesSync _google;
    private readonly ShopMediaService _media;

    public ShopsController(NtcaDbContext db, IImpressionTracker track, GooglePlacesSync google, ShopMediaService media)
    {
        _db = db;
        _track = track;
        _google = google;
        _media = media;
    }

    [HttpGet("/shops")]
    public async Task<IActionResult> Index(
        string? q,
        string? location,
        string? zip,
        string? state,
        string? specialty,
        string? sort,
        string? openNow,
        string? radiusMiles,
        string? view,
        double? lat,
        double? lng,
        int? page,
        CancellationToken ct)
    {
        var dir = ShopDirectory.Parse(q, location, zip, specialty, sort, openNow, radiusMiles, lat, lng, page);
        IQueryable<Shop> query = Active().Include(s => s.Tier).Include(s => s.Hours).Include(s => s.Socials).ThenInclude(x => x.Platform);
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.StateCode == state.Trim().ToUpperInvariant());
        var candidates = await query
            .OrderBy(s => s.Name)
            .Take(ShopDirectory.CandidateCap)
            .ToListAsync(ct);
        var origin = ShopDirectory.ResolveOrigin(candidates, dir);
        var oLat = origin?.Lat;
        var oLng = origin?.Lng;
        var matches = ShopDirectory.Apply(candidates, dir, oLat, oLng);
        var pageItems = ShopDirectory.TakePage(matches, dir.Page);
        var searching = dir.HasKeyword || dir.HasLocation || dir.Specialty.Length > 0 || dir.OpenNow;
        var surface = searching ? ImpressionSurfaces.DirectorySearch : ImpressionSurfaces.DirectoryList;
        _track.RecordMany(pageItems, surface, "/shops");

        ViewBag.Query = dir;
        ViewBag.Q = dir.Q;
        ViewBag.Location = dir.Location;
        ViewBag.State = state ?? "";
        ViewBag.Zip = dir.Location;
        ViewBag.Sort = ShopDirectory.ResolveSort(dir, oLat is not null);
        ViewBag.Specialty = dir.Specialty;
        ViewBag.OpenNow = dir.OpenNow;
        ViewBag.RadiusMiles = ShopDirectory.EffectiveRadius(dir, oLat is not null);
        ViewBag.OriginLat = oLat;
        ViewBag.OriginLng = oLng;
        ViewBag.OriginHow = oLat is null ? "" : (dir.Lat is not null ? "device" : "zip");
        ViewBag.Total = matches.Count;
        ViewBag.Page = dir.Page;
        ViewBag.PageCount = ShopDirectory.PageCount(matches.Count);
        ViewBag.Chips = ShopDirectory.Chips(dir, oLat is not null);
        ViewBag.HasOrigin = oLat is not null;
        ViewBag.LocationLabel = ShopDirectory.LocationLabel(dir, oLat is not null);
        ViewBag.Google = await _google.LoadViewsAsync(pageItems.Select(s => s.ShopId), ct);
        ViewBag.OwnerMedia = await _media.LoadPublicAsync(pageItems, ct);
        ViewData["MainClass"] = "site-main";
        ViewData["Title"] = "Find shops";
        return View(pageItems);
    }

    [HttpGet("/shops.md")]
    public async Task<IActionResult> DirectoryMarkdown(CancellationToken ct)
    {
        var shops = await Active().OrderBy(s => s.StateCode).ThenBy(s => s.Name).ToListAsync(ct);
        var origin = $"{Request.Scheme}://{Request.Host}";
        return Content(ShopMarkdown.Directory(shops, origin), "text/markdown; charset=utf-8");
    }

    [HttpGet("/shops/{state}")]
    public async Task<IActionResult> State(string state, CancellationToken ct)
    {
        var code = state.Trim().ToUpperInvariant();
        if (code.Length != 2)
            return NotFound();
        var shops = await Active().Include(s => s.Tier).Include(s => s.Hours).Include(s => s.Socials).ThenInclude(x => x.Platform)
            .Where(s => s.StateCode == code)
            .OrderBy(s => s.City).ThenBy(s => s.Name).ToListAsync(ct);
        _track.RecordMany(shops, ImpressionSurfaces.DirectoryState, "/shops/" + code.ToLowerInvariant());
        ViewBag.State = code;
        ViewBag.StateName = ShopCopy.StateName(code);
        ViewBag.JsonLd = ShopJsonLd.BuildStateHub($"{Request.Scheme}://{Request.Host}", code, shops.Count);
        ViewBag.Google = await _google.LoadViewsAsync(shops.Select(s => s.ShopId), ct);
        ViewBag.OwnerMedia = await _media.LoadPublicAsync(shops, ct);
        ViewData["MainClass"] = "site-main";
        return View(shops);
    }

    [HttpGet("/shops/{state}/{city}")]
    public async Task<IActionResult> City(string state, string city, CancellationToken ct)
    {
        var code = state.Trim().ToUpperInvariant();
        if (code.Length != 2)
            return NotFound();
        var slug = city.Trim().ToLowerInvariant();
        var shops = await Active().Include(s => s.Tier).Include(s => s.Hours).Include(s => s.Socials).ThenInclude(x => x.Platform)
            .Where(s => s.StateCode == code).ToListAsync(ct);
        shops = shops.Where(s => ShopCopy.CitySlug(s.City) == slug).OrderBy(s => s.Name).ToList();
        if (shops.Count == 0)
            return NotFound();
        _track.RecordMany(shops, ImpressionSurfaces.DirectoryState, $"/shops/{code.ToLowerInvariant()}/{slug}");
        ViewBag.State = code;
        ViewBag.StateName = ShopCopy.StateName(code);
        ViewBag.City = shops[0].City;
        ViewBag.Google = await _google.LoadViewsAsync(shops.Select(s => s.ShopId), ct);
        ViewBag.OwnerMedia = await _media.LoadPublicAsync(shops, ct);
        ViewData["MainClass"] = "site-main";
        return View(shops);
    }

    [HttpGet("/shops/{state}/{city}/{slug}.md")]
    public Task<IActionResult> DetailMarkdown(string state, string city, string slug, CancellationToken ct)
        => DetailCore(slug, markdown: true, ct);

    [HttpGet("/shops/{state}/{city}/{slug}")]
    public Task<IActionResult> Detail(string state, string city, string slug, string? format, CancellationToken ct)
        => DetailCore(slug, markdown: string.Equals(format, "md", StringComparison.OrdinalIgnoreCase), ct);

    [HttpGet("/api/v1/shops")]
    public async Task<IActionResult> Api(string? q, string? state, string? source, CancellationToken ct)
    {
        var query = Active();
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.StateCode == state.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.City.Contains(term));
        }
        var shops = await query.Include(s => s.Socials).ThenInclude(x => x.Platform)
            .OrderBy(s => s.Name).Take(200).ToListAsync(ct);
        if (string.Equals(source, "app", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "maui", StringComparison.OrdinalIgnoreCase))
            _track.RecordMany(shops, ImpressionSurfaces.ApiApp, "/api/v1/shops");
        return Json(shops.Select(s => new
        {
            s.PublicId,
            s.Slug,
            s.Name,
            s.City,
            s.StateCode,
            s.Phone,
            s.WebsiteUrl,
            loc = ShopCopy.Loc(s),
            lat = s.Latitude,
            lng = s.Longitude,
            destination = s.IsDestinationShop,
            socials = ShopSocialLinks.Resolve(s).Select(l => new
            {
                platform = l.PlatformCode,
                label = l.Label,
                url = l.Url,
                handle = l.Handle
            })
        }));
    }

    private async Task<IActionResult> DetailCore(string slug, bool markdown, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking()
            .Include(s => s.Tier)
            .Include(s => s.Hours)
            .Include(s => s.Socials).ThenInclude(x => x.Platform)
            .FirstOrDefaultAsync(s => s.Slug == slug && s.ListingStatus == "ACTIVE", ct);
        if (shop is null)
            return NotFound();

        var hours = shop.Hours.OrderBy(h => h.DayOfWeek).ToList();
        var socials = ShopSocialLinks.Resolve(shop);
        var events = await _db.ShopEvents.AsNoTracking()
            .Where(e => e.ShopId == shop.ShopId && e.Status == "PUBLISHED" && e.StartsAt >= DateTime.UtcNow)
            .OrderBy(e => e.StartsAt)
            .Take(12)
            .ToListAsync(ct);
        var reviews = await _db.ShopReviews.AsNoTracking()
            .Where(r => r.ShopId == shop.ShopId && r.ModerationStatus == "APPROVED")
            .Select(r => (int)r.Rating)
            .ToListAsync(ct);
        var offers = ServiceOffers(shop, events);
        var origin = $"{Request.Scheme}://{Request.Host}";
        var canonical = origin + ShopCopy.Loc(shop);
        var summary = ShopCopy.Summary(shop, hours);
        var google = (await _google.LoadViewsAsync([shop.ShopId], ct)).GetValueOrDefault(shop.ShopId);
        var ownerMedia = (await _media.LoadPublicAsync([shop], ct)).GetValueOrDefault(shop.ShopId);
        var leadPhoto = google?.Photos.FirstOrDefault();
        var imageUrl = ownerMedia?.CoverLargeUrl
            ?? (leadPhoto is null ? null : origin + "/media/gplace/" + shop.ShopId + "/" + leadPhoto.Slot);
        var jsonLd = ShopJsonLd.Build(new ShopSchemaInput
        {
            Shop = shop,
            Origin = origin,
            CanonicalUrl = canonical,
            AiSummary = summary,
            TierName = shop.Tier?.Name,
            Hours = hours,
            UpcomingEvents = events,
            ApprovedReviewCount = reviews.Count,
            AverageRating = reviews.Count == 0 ? null : (decimal)reviews.Average(),
            Offers = offers,
            ImageUrl = imageUrl,
            SameAs = socials.Where(l => l.IsSameAs).Select(l => l.Url).ToList()
        });

        if (markdown)
            return Content(ShopMarkdown.Render(shop, summary, hours, events, shop.Tier?.Name, socials), "text/markdown; charset=utf-8");

        _track.Record(shop.ShopId, ImpressionSurfaces.Detail, pageUrl: ShopCopy.Loc(shop));
        ViewData["Title"] = shop.Name;
        ViewData["Description"] = summary;
        ViewData["MainClass"] = "site-main";
        ViewData["BodyClass"] = "is-shop-detail";
        ViewData["HideDock"] = true;
        return View(new ShopDetailVm
        {
            Shop = shop,
            AiSummary = summary,
            JsonLd = jsonLd,
            MarkdownUrl = ShopCopy.MdLoc(shop),
            Hours = hours,
            Events = events,
            TierName = shop.Tier?.Name,
            Services = ShopCopy.ServiceLabels(shop, events),
            Google = google,
            Socials = socials,
            LogoUrl = ownerMedia?.LogoUrl,
            CoverUrl = ownerMedia?.CoverLargeUrl,
            Photos = ownerMedia?.Photos ?? []
        });
    }

    private IQueryable<Shop> Active()
        => _db.Shops.AsNoTracking().Where(s => s.ListingStatus == "ACTIVE");

    private static IReadOnlyList<ShopServiceOffer> ServiceOffers(Shop shop, IReadOnlyList<ShopEvent> events)
    {
        var offers = new List<ShopServiceOffer>();
        if (events.Any(e => e.EventType == "BREAK"))
            offers.Add(new ShopServiceOffer { Name = "Live card breaks", Description = "In-store or hosted card breaks." });
        if (events.Any(e => e.EventType == "TRADE_NIGHT"))
            offers.Add(new ShopServiceOffer { Name = "Buy / sell / trade nights" });
        return offers;
    }
}

public sealed class ShopDetailVm
{
    public required Shop Shop { get; init; }
    public required string AiSummary { get; init; }
    public required string JsonLd { get; init; }
    public required string MarkdownUrl { get; init; }
    public IReadOnlyList<ShopHours> Hours { get; init; } = [];
    public IReadOnlyList<ShopEvent> Events { get; init; } = [];
    public IReadOnlyList<string> Services { get; init; } = [];
    public string? TierName { get; init; }
    public GooglePlaceView? Google { get; init; }
    public IReadOnlyList<ShopSocialLink> Socials { get; init; } = [];
    public string? LogoUrl { get; init; }
    public string? CoverUrl { get; init; }
    public IReadOnlyList<ShopPhotoView> Photos { get; init; } = [];
}
