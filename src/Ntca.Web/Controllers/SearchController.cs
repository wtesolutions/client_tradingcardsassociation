using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class SearchController : Controller
{
    private readonly NtcaDbContext _db;
    public SearchController(NtcaDbContext db) => _db = db;

    [HttpGet("/api/v1/search/shops")]
    [EnableRateLimiting("directory-search")]
    public async Task<IActionResult> Shops(string? q, int? page, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return Json(DirectorySearch.PageResult(Array.Empty<object>(), DirectorySearch.Page(page), false));
        var query = DirectorySearch.FilterShops(
            _db.Shops.AsNoTracking().Where(s => s.ListingStatus == "ACTIVE"), q!);
        var (rows, more) = await DirectorySearch.TakePageAsync(query.OrderBy(s => s.Name), page, ct);
        return Json(DirectorySearch.PageResult(rows.Select(s => new
        {
            id = s.ShopId,
            name = s.Name,
            city = s.City,
            state = s.StateCode,
            zip = s.PostalCode,
            detail = $"{s.City}, {s.StateCode}" + (string.IsNullOrWhiteSpace(s.PostalCode) ? "" : " " + s.PostalCode)
        }).ToList(), DirectorySearch.Page(page), more));
    }

    [HttpGet("/api/v1/search/shows")]
    [EnableRateLimiting("directory-search")]
    public async Task<IActionResult> Shows(string? q, int? page, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return Json(DirectorySearch.PageResult(Array.Empty<object>(), DirectorySearch.Page(page), false));
        var query = DirectorySearch.FilterShows(
            _db.ShopEvents.AsNoTracking().Include(e => e.Venue)
                .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED")), q!);
        var (rows, more) = await DirectorySearch.TakePageAsync(query.OrderBy(e => e.StartsAt), page, ct);
        return Json(DirectorySearch.PageResult(rows.Select(e => new
        {
            id = e.ShopEventId,
            name = e.Title,
            city = e.City,
            state = e.StateCode,
            zip = e.Venue?.PostalCode,
            detail = $"{e.City}, {e.StateCode} · {e.StartsAt:MMM d, yyyy}"
        }).ToList(), DirectorySearch.Page(page), more));
    }

    [HttpGet("/api/v1/search/promoters")]
    [EnableRateLimiting("directory-search")]
    public async Task<IActionResult> Promoters(string? q, int? page, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return Json(DirectorySearch.PageResult(Array.Empty<object>(), DirectorySearch.Page(page), false));
        var query = DirectorySearch.FilterPromoters(_db.Promoters.AsNoTracking(), q!);
        var (rows, more) = await DirectorySearch.TakePageAsync(query.OrderBy(p => p.Name), page, ct);
        return Json(DirectorySearch.PageResult(rows.Select(p => new
        {
            id = p.PromoterId,
            name = p.Name,
            detail = p.WebsiteUrl ?? ""
        }).ToList(), DirectorySearch.Page(page), more));
    }

    [HttpGet("/api/v1/search/manufacturers")]
    [EnableRateLimiting("directory-search")]
    public async Task<IActionResult> Manufacturers(string? q, int? page, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return Json(DirectorySearch.PageResult(Array.Empty<object>(), DirectorySearch.Page(page), false));
        var query = DirectorySearch.FilterManufacturers(
            _db.Manufacturers.AsNoTracking().Where(m => m.IsActive), q!);
        var (rows, more) = await DirectorySearch.TakePageAsync(query.OrderBy(m => m.Name), page, ct);
        return Json(DirectorySearch.PageResult(rows.Select(m => new
        {
            id = m.ManufacturerId,
            name = m.Name,
            detail = m.CountryName ?? ""
        }).ToList(), DirectorySearch.Page(page), more));
    }
}
