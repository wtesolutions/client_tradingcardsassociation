using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class DashboardController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly SiteModeService _mode;

    public DashboardController(NtcaDbContext db, SiteModeService mode)
    {
        _db = db;
        _mode = mode;
    }

    [HttpGet("/SADMIN/dashboard")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var vm = new SadminDashboardVm
        {
            Shops = await _db.Shops.CountAsync(s => s.ListingStatus == "ACTIVE", ct),
            UpcomingShows = await _db.ShopEvents.CountAsync(e =>
                e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED") && e.StartsAt >= now, ct),
            PendingEvents = await _db.EventSubmissions.CountAsync(s => s.Status == "PENDING_REVIEW", ct),
            PendingEdits = await _db.ShopEditRequests.CountAsync(s => s.Status == "PENDING_REVIEW", ct),
            Signups7d = await _db.Signups.CountAsync(s => s.CreatedAt >= weekAgo, ct),
            Manufacturers = await _db.Manufacturers.CountAsync(m => m.IsActive, ct),
            DraftPosts = await _db.Posts.CountAsync(p => p.Status == "DRAFT", ct),
            Mode = await _mode.GetModeAsync(ct),
            LatestShops = await _db.Shops.AsNoTracking().OrderByDescending(s => s.CreatedAt).Take(6).ToListAsync(ct),
            Upcoming = await _db.ShopEvents.AsNoTracking()
                .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED") && e.StartsAt >= now)
                .OrderBy(e => e.StartsAt).Take(6).ToListAsync(ct)
        };
        return View(vm);
    }

    [HttpPost("/SADMIN/dashboard/mode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMode(string mode, CancellationToken ct)
    {
        if (mode is "Preview" or "Live")
            await _mode.SetModeAsync(mode, ct);
        return Redirect("/SADMIN/dashboard");
    }
}

public sealed class SadminDashboardVm
{
    public int Shops { get; set; }
    public int UpcomingShows { get; set; }
    public int PendingEvents { get; set; }
    public int PendingEdits { get; set; }
    public int Signups7d { get; set; }
    public int Manufacturers { get; set; }
    public int DraftPosts { get; set; }
    public string Mode { get; set; } = "Preview";
    public List<Ntca.Core.Shop> LatestShops { get; set; } = [];
    public List<Ntca.Core.ShopEvent> Upcoming { get; set; } = [];
}
