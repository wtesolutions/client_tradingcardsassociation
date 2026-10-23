using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class CalendarController : Controller
{
    private readonly NtcaDbContext _db;
    public CalendarController(NtcaDbContext db) => _db = db;

    [HttpGet("/calendar")]
    [HttpGet("/shows")]
    public async Task<IActionResult> Index(string? state, string? q, string? scope, string? view, int? year, int? month, CancellationToken ct)
    {
        view = (view ?? "month").Trim().ToLowerInvariant();
        if (view is not ("month" or "list" or "map"))
            view = "month";

        var query = Filter(state, q, scope);
        List<ShopEvent> rows;
        if (view == "month")
        {
            var (y, m) = await ResolveMonthAsync(query, year, month, ct);
            var start = new DateTime(y, m, 1);
            var end = start.AddMonths(1);
            rows = await query
                .Where(e => e.StartsAt < end && (e.EndsAt ?? e.StartsAt) >= start)
                .OrderBy(e => e.StartsAt)
                .ToListAsync(ct);
            ViewBag.Year = y;
            ViewBag.Month = m;
            ViewBag.MonthStart = start;
        }
        else
        {
            rows = await query.OrderBy(e => e.StartsAt).Take(200).ToListAsync(ct);
        }

        ViewBag.View = view;
        ViewBag.State = state ?? "";
        ViewBag.Q = q ?? "";
        ViewBag.Scope = scope ?? "";
        ViewBag.States = await _db.ShopEvents.AsNoTracking()
            .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED") && e.StateCode != null)
            .Select(e => e.StateCode!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
        ViewBag.MapPoints = rows
            .Where(e => e.Venue?.Latitude is not null && e.Venue?.Longitude is not null)
            .Select(e => new
            {
                lat = e.Venue!.Latitude,
                lng = e.Venue.Longitude,
                title = e.Title,
                loc = Loc(e),
                when = e.StartsAt.ToString("MMM d, yyyy")
            })
            .ToList();
        return View(rows);
    }

    [HttpGet("/shows/{state}")]
    public async Task<IActionResult> State(string state, CancellationToken ct)
    {
        var code = state.Trim().ToUpperInvariant();
        var rows = await Filter(code, null, null).OrderBy(e => e.StartsAt).ToListAsync(ct);
        ViewBag.State = code;
        return View("State", rows);
    }

    [HttpGet("/shows/{state}/{city}/{slug}")]
    public async Task<IActionResult> Detail(string state, string city, string slug, CancellationToken ct)
    {
        var ev = await _db.ShopEvents.AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Promoter)
            .Include(e => e.Shop)
            .FirstOrDefaultAsync(e => e.Slug == slug, ct);
        if (ev is null)
            return NotFound();
        return View(ev);
    }

    [HttpGet("/calendar.ics")]
    public async Task<IActionResult> IcsFeed(string? state, string? q, string? scope, CancellationToken ct)
    {
        var rows = await Filter(state, q, scope).OrderBy(e => e.StartsAt).Take(500).ToListAsync(ct);
        var name = string.IsNullOrWhiteSpace(state) ? "NTCA card shows" : $"NTCA card shows — {state.Trim().ToUpperInvariant()}";
        return IcsFile(CalendarIcs.Feed(rows, name, Origin()), "ntca-calendar.ics");
    }

    [HttpGet("/shows/{state}.ics")]
    public Task<IActionResult> StateIcs(string state, CancellationToken ct)
        => IcsFeed(state, null, null, ct);

    [HttpGet("/shows/{state}/{city}/{slug}.ics")]
    public async Task<IActionResult> ShowIcs(string state, string city, string slug, CancellationToken ct)
    {
        var ev = await _db.ShopEvents.AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Promoter)
            .FirstOrDefaultAsync(e => e.Slug == slug, ct);
        if (ev is null)
            return NotFound();
        return IcsFile(CalendarIcs.Feed(new[] { ev }, ev.Title, Origin()), slug + ".ics");
    }

    [HttpGet("/api/v1/calendar")]
    public async Task<IActionResult> Api(string? state, string? q, string? scope, CancellationToken ct)
    {
        var rows = await Filter(state, q, scope).OrderBy(e => e.StartsAt).Take(200).ToListAsync(ct);
        return Json(rows.Select(e => new
        {
            e.PublicId,
            e.Slug,
            e.Title,
            e.StartsAt,
            e.EndsAt,
            e.Scope,
            e.Status,
            e.IsFeatured,
            e.City,
            e.StateCode,
            venue = e.Venue?.Name,
            promoter = e.Promoter?.Name,
            loc = Loc(e),
            lat = e.Venue?.Latitude,
            lng = e.Venue?.Longitude
        }));
    }

    private IQueryable<ShopEvent> Filter(string? state, string? q, string? scope)
    {
        var query = _db.ShopEvents.AsNoTracking()
            .Include(e => e.Venue)
            .Include(e => e.Promoter)
            .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED"));
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(e => e.StateCode == state.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(scope))
            query = query.Where(e => e.Scope == scope.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.Title.Contains(term) || (e.City != null && e.City.Contains(term)));
        }
        return query;
    }

    private static async Task<(int Year, int Month)> ResolveMonthAsync(IQueryable<ShopEvent> query, int? year, int? month, CancellationToken ct)
    {
        if (year is >= 2000 and <= 2100 && month is >= 1 and <= 12)
            return (year.Value, month.Value);
        var now = DateTime.UtcNow.Date;
        var next = await query.Where(e => e.StartsAt >= now).OrderBy(e => e.StartsAt).Select(e => e.StartsAt).FirstOrDefaultAsync(ct);
        if (next == default)
            next = await query.OrderBy(e => e.StartsAt).Select(e => e.StartsAt).FirstOrDefaultAsync(ct);
        if (next == default)
            return (now.Year, now.Month);
        return (next.Year, next.Month);
    }

    public static string Loc(ShopEvent e)
    {
        var st = (e.StateCode ?? e.Venue?.StateCode ?? "xx").ToLowerInvariant();
        var city = (e.City ?? e.Venue?.City ?? "city").ToLowerInvariant().Replace(' ', '-').Replace(".", "");
        return $"/shows/{st}/{city}/{e.Slug}";
    }

    private string Origin()
        => $"{Request.Scheme}://{Request.Host}";

    private static FileContentResult IcsFile(string body, string downloadName)
        => new(Encoding.UTF8.GetBytes(body), "text/calendar; charset=utf-8")
        {
            FileDownloadName = downloadName
        };
}
