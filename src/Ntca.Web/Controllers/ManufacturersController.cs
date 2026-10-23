using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class ManufacturersController : Controller
{
    private readonly NtcaDbContext _db;
    public ManufacturersController(NtcaDbContext db) => _db = db;

    [HttpGet("/manufacturers")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = _db.Manufacturers.AsNoTracking().Where(m => m.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m => m.Name.Contains(term) || m.NormalizedName.Contains(term));
        }
        var rows = await query.OrderBy(m => m.Name).ToListAsync(ct);
        ViewBag.Q = q ?? "";
        ViewData["MainClass"] = "site-main";
        return View(rows);
    }

    [HttpGet("/manufacturers/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var mfr = await _db.Manufacturers.AsNoTracking()
            .Include(m => m.Categories).ThenInclude(c => c.Category)
            .Include(m => m.Topics)
            .FirstOrDefaultAsync(m => m.Slug == slug, ct);
        if (mfr is null)
            return NotFound();
        ViewData["MainClass"] = "site-main";
        return View(mfr);
    }
}
