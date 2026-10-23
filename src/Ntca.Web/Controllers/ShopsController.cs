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

    public ShopsController(NtcaDbContext db, IImpressionTracker track)
    {
        _db = db;
        _track = track;
    }

    [HttpGet("/shops")]
    public async Task<IActionResult> Index(string? q, string? state, string? view, string? zip, string? sort, double? lat, double? lng, CancellationToken ct)
    {
        IQueryable<Shop> query = Active().Include(s => s.Tier);
        var searching = !string.IsNullOrWhiteSpace(q);
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.StateCode == state.Trim().ToUpperInvariant());
        if (searching)
        {
            var term = q!.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.City.Contains(term) || (s.PostalCode != null && s.PostalCode.StartsWith(term)));
        }
        var shops = await query.OrderBy(s => s.StateCode).ThenBy(s => s.City).ThenBy(s => s.Name).Take(400).ToListAsync(ct);
        var (oLat, oLng, originHow) = ResolveOrigin(shops, zip, lat, lng);
        if (string.Equals(sort, "distance", StringComparison.OrdinalIgnoreCase) && oLat is not null)
        {
            shops = shops
                .OrderBy(s => ShopCopy.MilesFrom(s, oLat, oLng) ?? double.MaxValue)
                .ThenBy(s => s.Name)
                .ToList();
        }
        var surface = searching ? ImpressionSurfaces.DirectorySearch
            : view == "map" ? ImpressionSurfaces.MapPin
            : ImpressionSurfaces.DirectoryList;
        if (view == "map")
            _track.RecordMany(shops.Where(s => s.Latitude is not null && s.Longitude is not null).ToList(), surface, "/shops");
        else
            _track.RecordMany(shops, surface, "/shops");

        ViewBag.Q = q ?? "";
        ViewBag.State = state ?? "";
        ViewBag.Zip = zip ?? "";
        ViewBag.Sort = sort ?? "";
        ViewBag.View = view ?? "list";
        ViewBag.OriginLat = oLat;
        ViewBag.OriginLng = oLng;
        ViewBag.OriginHow = originHow;
        ViewBag.States = await _db.Shops.AsNoTracking()
            .Where(s => s.ListingStatus == "ACTIVE")
            .Select(s => s.StateCode)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
        ViewBag.MapPoints = shops
            .Where(s => s.Latitude is not null && s.Longitude is not null)
            .Select(s => new
            {
                lat = s.Latitude,
                lng = s.Longitude,
                title = s.Name,
                loc = ShopCopy.Loc(s),
                where = s.City + ", " + s.StateCode
            })
            .ToList();
        return View(shops);
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
        var shops = await Active().Include(s => s.Tier).Where(s => s.StateCode == code)
            .OrderBy(s => s.City).ThenBy(s => s.Name).ToListAsync(ct);
        _track.RecordMany(shops, ImpressionSurfaces.DirectoryState, "/shops/" + code.ToLowerInvariant());
        ViewBag.State = code;
        ViewBag.StateName = ShopCopy.StateName(code);
        return View(shops);
    }

    [HttpGet("/shops/{state}/{city}")]
    public async Task<IActionResult> City(string state, string city, CancellationToken ct)
    {
        var code = state.Trim().ToUpperInvariant();
        if (code.Length != 2)
            return NotFound();
        var slug = city.Trim().ToLowerInvariant();
        var shops = await Active().Include(s => s.Tier).Where(s => s.StateCode == code).ToListAsync(ct);
        shops = shops.Where(s => ShopCopy.CitySlug(s.City) == slug).OrderBy(s => s.Name).ToList();
        if (shops.Count == 0)
            return NotFound();
        _track.RecordMany(shops, ImpressionSurfaces.DirectoryState, $"/shops/{code.ToLowerInvariant()}/{slug}");
        ViewBag.State = code;
        ViewBag.StateName = ShopCopy.StateName(code);
        ViewBag.City = shops[0].City;
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
        var shops = await query.OrderBy(s => s.Name).Take(200).ToListAsync(ct);
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
            destination = s.IsDestinationShop
        }));
    }

    private async Task<IActionResult> DetailCore(string slug, bool markdown, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking()
            .Include(s => s.Tier)
            .Include(s => s.Hours)
            .FirstOrDefaultAsync(s => s.Slug == slug && s.ListingStatus == "ACTIVE", ct);
        if (shop is null)
            return NotFound();

        var hours = shop.Hours.OrderBy(h => h.DayOfWeek).ToList();
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
            Offers = offers
        });

        if (markdown)
            return Content(ShopMarkdown.Render(shop, summary, hours, events, shop.Tier?.Name), "text/markdown; charset=utf-8");

        _track.Record(shop.ShopId, ImpressionSurfaces.Detail, pageUrl: ShopCopy.Loc(shop));
        ViewData["Title"] = shop.Name;
        ViewData["Description"] = summary;
        return View(new ShopDetailVm
        {
            Shop = shop,
            AiSummary = summary,
            JsonLd = jsonLd,
            MarkdownUrl = ShopCopy.MdLoc(shop),
            Hours = hours,
            Events = events,
            TierName = shop.Tier?.Name
        });
    }

    private IQueryable<Shop> Active()
        => _db.Shops.AsNoTracking().Where(s => s.ListingStatus == "ACTIVE");

    private static (double? Lat, double? Lng, string How) ResolveOrigin(IReadOnlyList<Shop> shops, string? zip, double? lat, double? lng)
    {
        if (lat is >= -90 and <= 90 && lng is >= -180 and <= 180)
            return (lat, lng, "device");
        var fromZip = ShopCopy.OriginFromZip(shops, zip);
        if (fromZip is { } z)
            return (z.Lat, z.Lng, "zip");
        return (null, null, "");
    }

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
    public string? TierName { get; init; }
}
