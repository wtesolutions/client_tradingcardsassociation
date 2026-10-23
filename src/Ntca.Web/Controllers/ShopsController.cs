using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class ShopsController : Controller
{
    private readonly NtcaDbContext _db;
    public ShopsController(NtcaDbContext db) => _db = db;

    [HttpGet("/shops")]
    public async Task<IActionResult> Index(string? q, string? state, CancellationToken ct)
    {
        var query = _db.Shops.AsNoTracking().Where(s => s.ListingStatus == "ACTIVE");
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.StateCode == state.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.City.Contains(term));
        }
        var shops = await query.OrderBy(s => s.StateCode).ThenBy(s => s.City).ThenBy(s => s.Name).Take(400).ToListAsync(ct);
        ViewBag.Q = q ?? "";
        ViewBag.State = state ?? "";
        ViewBag.States = await _db.Shops.AsNoTracking()
            .Where(s => s.ListingStatus == "ACTIVE")
            .Select(s => s.StateCode)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);
        return View(shops);
    }

    [HttpGet("/shops/{state}/{city}/{slug}")]
    public async Task<IActionResult> Detail(string state, string city, string slug, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == slug && s.ListingStatus == "ACTIVE", ct);
        if (shop is null)
            return NotFound();
        return View(shop);
    }
}
