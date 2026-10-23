using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly SiteModeService _mode;

    public HomeController(NtcaDbContext db, SiteModeService mode)
    {
        _db = db;
        _mode = mode;
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
        ViewBag.ShopCount = await _db.Shops.CountAsync(s => s.ListingStatus == "ACTIVE", ct);
        ViewBag.ShowCount = await _db.ShopEvents.CountAsync(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED"), ct);
        ViewBag.MfrCount = await _db.Manufacturers.CountAsync(m => m.IsActive, ct);
        ViewBag.IsPreview = await _mode.IsPreviewAsync(ct);
        return View((shows, posts));
    }

    public IActionResult Error() => View();
}
