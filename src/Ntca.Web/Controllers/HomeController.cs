using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly SiteModeService _mode;
    private readonly IImpressionTracker _track;

    public HomeController(NtcaDbContext db, SiteModeService mode, IImpressionTracker track)
    {
        _db = db;
        _mode = mode;
        _track = track;
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
            .Where(s => s.ListingStatus == "ACTIVE" && (s.SourceSystem == "TOP100-2026-10-22" || (s.ImportNotes != null && s.ImportNotes.Contains("Destination shop"))))
            .OrderBy(s => s.Name)
            .Take(6)
            .ToListAsync(ct);
        var rotation = await _db.Shops.AsNoTracking()
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
        ViewData["MainClass"] = "site-main";
        return View((shows, posts));
    }

    [HttpGet("/about")]
    public IActionResult About()
    {
        ViewData["Title"] = "About NTCA";
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
