using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class GoogleController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly GooglePlacesSync _sync;
    private readonly GooglePlacesOptions _opt;

    public GoogleController(NtcaDbContext db, GooglePlacesSync sync, IOptions<GooglePlacesOptions> opt)
    {
        _db = db;
        _sync = sync;
        _opt = opt.Value;
    }

    [HttpGet("/SADMIN/google")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Google Places";
        var status = await _sync.StatusAsync(ct);
        var usage = await _sync.UsageAsync(ct);
        var log = await _sync.CallLogAsync(14, ct);
        List<GooglePlaceMatch> review;
        try
        {
            review = await _db.GooglePlaceMatches.AsNoTracking()
                .Include(m => m.Shop)
                .Where(m => m.MatchStatus == "PENDING_REVIEW")
                .OrderByDescending(m => m.Confidence)
                .Take(100)
                .ToListAsync(ct);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            review = [];
        }
        return View(new GoogleAdminVm
        {
            KeyConfigured = _opt.HasKey,
            Enabled = _opt.Enabled,
            DailyLimit = _opt.DailyCallLimit,
            MonthlyLimit = _opt.MonthlyCallLimit,
            TodayCalls = usage.Today,
            MonthCalls = usage.Month,
            Unmatched = status.Unmatched,
            PendingReview = status.Pending,
            Matched = status.Matched,
            Rejected = status.Rejected,
            LastRefresh = status.LastRefresh,
            CallLog = log,
            Review = review,
            Result = TempData["GoogleResult"] as string
        });
    }

    [HttpPost("/SADMIN/google/match")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Match(bool dryRun, int? take, CancellationToken ct)
    {
        var result = await _sync.MatchAsync(dryRun, take, ct);
        TempData["GoogleResult"] = Format(result);
        return Redirect("/SADMIN/google");
    }

    [HttpPost("/SADMIN/google/refresh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(bool dryRun, string? shopIds, CancellationToken ct)
    {
        var ids = ParseIds(shopIds);
        var result = await _sync.RefreshAsync(dryRun, ids.Count == 0 ? null : ids, take: ids.Count == 0 ? null : ids.Count, ct);
        TempData["GoogleResult"] = Format(result);
        return Redirect("/SADMIN/google");
    }

    [HttpPost("/SADMIN/google/review/{shopId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(long shopId, string decision, string? placeId, CancellationToken ct)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _sync.ApplyReviewAsync(shopId, decision, placeId, userId, ct);
        TempData["GoogleResult"] = "Review saved for shop " + shopId + " (" + decision + ").";
        return Redirect("/SADMIN/google");
    }

    private static List<long> ParseIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();
    }

    private static string Format(GooglePlacesRunResult r)
        => $"{r.Message} Plan: Text Search {r.Planned.TextSearch}, Details {r.Planned.PlaceDetails}, Photos {r.Planned.PlacePhotos} (total {r.Planned.Total}). Used {r.CallsUsed}. Dry-run={r.DryRun} No-op={r.NoOp} Limit={r.HitLimit}";
}

public sealed class GoogleAdminVm
{
    public bool KeyConfigured { get; init; }
    public bool Enabled { get; init; }
    public int DailyLimit { get; init; }
    public int MonthlyLimit { get; init; }
    public int TodayCalls { get; init; }
    public int MonthCalls { get; init; }
    public int Unmatched { get; init; }
    public int PendingReview { get; init; }
    public int Matched { get; init; }
    public int Rejected { get; init; }
    public DateTime? LastRefresh { get; init; }
    public IReadOnlyList<(DateTime Day, string Sku, int Count)> CallLog { get; init; } = [];
    public IReadOnlyList<GooglePlaceMatch> Review { get; init; } = [];
    public string? Result { get; init; }
}
