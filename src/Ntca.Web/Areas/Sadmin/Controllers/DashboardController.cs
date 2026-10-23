using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;
using Ntca.Core;

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
        var totals7 = await ShopStatQueries.TotalsAsync(_db, null, 7, ct);
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
                .OrderBy(e => e.StartsAt).Take(6).ToListAsync(ct),
            Impressions7d = totals7.Impressions,
            Views7d = totals7.Views,
            Clicks7d = totals7.Clicks,
            TopShops = await TopShopsAsync(7, 8, ct)
        };
        return View(vm);
    }

    private async Task<List<ShopRankRow>> TopShopsAsync(int days, int take, CancellationToken ct)
    {
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var rows = await _db.ShopImpressions.AsNoTracking()
            .Where(i => !i.IsBot && i.CreatedAt >= from && !i.Surface.StartsWith("CLICK_"))
            .GroupBy(i => i.ShopId)
            .Select(g => new { ShopId = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N)
            .Take(take)
            .ToListAsync(ct);
        var ids = rows.Select(r => r.ShopId).ToList();
        var shops = await _db.Shops.AsNoTracking().Where(s => ids.Contains(s.ShopId)).ToDictionaryAsync(s => s.ShopId, ct);
        return rows.Select(r => new ShopRankRow
        {
            Shop = shops.GetValueOrDefault(r.ShopId),
            Impressions = r.N
        }).Where(r => r.Shop is not null).ToList();
    }

    [HttpGet("/SADMIN/shops/{id:long}/stats")]
    public async Task<IActionResult> ShopStats(long id, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == id, ct);
        if (shop is null)
            return NotFound();
        ViewBag.Stats7 = await ShopStatQueries.TotalsAsync(_db, id, 7, ct);
        ViewBag.Stats30 = await ShopStatQueries.TotalsAsync(_db, id, 30, ct);
        ViewBag.Stats90 = await ShopStatQueries.TotalsAsync(_db, id, 90, ct);
        ViewBag.Daily = await ShopStatQueries.DailyAsync(_db, id, 30, ct);
        return View(shop);
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
    public int Impressions7d { get; set; }
    public int Views7d { get; set; }
    public int Clicks7d { get; set; }
    public List<ShopRankRow> TopShops { get; set; } = [];
}

public sealed class ShopRankRow
{
    public Shop? Shop { get; set; }
    public int Impressions { get; set; }
}
