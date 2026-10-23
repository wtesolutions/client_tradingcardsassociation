using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly SiteModeService _mode;
    private readonly IImpressionTracker _track;
    private readonly GooglePlacesSync _google;
    private readonly IFileStorage _files;

    public HomeController(NtcaDbContext db, SiteModeService mode, IImpressionTracker track, GooglePlacesSync google, IFileStorage files)
    {
        _db = db;
        _mode = mode;
        _track = track;
        _google = google;
        _files = files;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var shows = await _db.ShopEvents.AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Promoter)
            .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED") && e.StartsAt >= now)
            .OrderByDescending(e => e.IsFeatured)
            .ThenBy(e => e.StartsAt)
            .Take(6)
            .ToListAsync(ct);
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => p.Status == "PUBLISHED")
            .OrderByDescending(p => p.PublishedAt)
            .Take(3)
            .ToListAsync(ct);
        var featured = await _db.Shops.AsNoTracking()
            .Include(s => s.Socials).ThenInclude(x => x.Platform)
            .Where(s => s.ListingStatus == "ACTIVE" && (s.SourceSystem == "TOP100-2026-10-22" || (s.ImportNotes != null && s.ImportNotes.Contains("Destination shop"))))
            .OrderBy(s => s.Name)
            .Take(6)
            .ToListAsync(ct);
        var rotation = await _db.Shops.AsNoTracking()
            .Include(s => s.Socials).ThenInclude(x => x.Platform)
            .Where(s => s.ListingStatus == "ACTIVE")
            .OrderByDescending(s => s.PublishedAt ?? s.CreatedAt)
            .Take(6)
            .ToListAsync(ct);
        _track.RecordMany(featured, ImpressionSurfaces.Featured, "/");
        _track.RecordMany(rotation, ImpressionSurfaces.HomeRotation, "/");
        ViewBag.ShopCount = await _db.Shops.CountAsync(s => s.ListingStatus == "ACTIVE", ct);
        ViewBag.ShowCount = await _db.ShopEvents.CountAsync(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED"), ct);
        ViewBag.MfrCount = await _db.Manufacturers.CountAsync(m => m.IsActive, ct);
        ViewBag.IsPreview = await _mode.IsPreviewAsync(ct);
        ViewBag.Featured = featured;
        ViewBag.Rotation = rotation;
        ViewBag.Google = await _google.LoadViewsAsync(featured.Concat(rotation).Select(s => s.ShopId), ct);
        var gallery = await _db.GalleryItems.AsNoTracking()
            .Where(g => g.IsPublished)
            .OrderBy(g => g.DisplayOrder)
            .Take(6)
            .ToListAsync(ct);
        ViewBag.Gallery = gallery;
        ViewBag.Files = _files;
        ViewBag.Kickoff = await _db.AssociationEvents.AsNoTracking()
            .Where(e => e.Status == "PUBLISHED" && e.Slug == "ntca-community-kickoff")
            .FirstOrDefaultAsync(ct);
        ViewData["MainClass"] = "site-main";
        return View((shows, posts));
    }

    [HttpGet("/about")]
    public async Task<IActionResult> About(CancellationToken ct)
    {
        var people = await _db.PersonBios.AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);
        var gallery = await _db.GalleryItems.AsNoTracking()
            .Where(g => g.IsPublished)
            .OrderBy(g => g.DisplayOrder)
            .Take(4)
            .ToListAsync(ct);
        ViewData["Title"] = "About NTCA";
        ViewData["Description"] = "The mission, vision, values, history and people of the National Trading Card Association.";
        ViewData["MainClass"] = "site-main";
        ViewBag.People = people;
        ViewBag.Gallery = gallery;
        ViewBag.Files = _files;
        ViewBag.BoysUrl = _files.PublicUrl("assets/about-boys-cards.jpg");
        ViewBag.VintageUrl = _files.PublicUrl("assets/vintage-cards.jpeg");
        ViewBag.FounderShowUrl = _files.PublicUrl("assets/founder-mantle.jpeg");
        ViewBag.MascotsUrl = _files.PublicUrl("assets/brody-skylar.png");
        return View();
    }

    [HttpGet("/saved")]
    public IActionResult Saved()
    {
        ViewData["Title"] = "Saved shops";
        return View();
    }

    public IActionResult Error() => View();
}
