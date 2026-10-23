using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class ManufacturersController : Controller
{
    private readonly NtcaDbContext _db;
    public ManufacturersController(NtcaDbContext db) => _db = db;

    [HttpGet("/SADMIN/manufacturers")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = _db.Manufacturers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m => m.Name.Contains(term) || m.Slug.Contains(term) || m.NormalizedName.Contains(term));
        }
        ViewData["Title"] = "Manufacturers";
        ViewBag.Q = q ?? "";
        return View(await query.OrderBy(m => m.Name).Take(400).ToListAsync(ct));
    }

    [HttpGet("/SADMIN/manufacturers/{id:long}")]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var row = await _db.Manufacturers.AsNoTracking().FirstOrDefaultAsync(m => m.ManufacturerId == id, ct);
        if (row is null)
            return NotFound();
        ViewData["Title"] = "Edit · " + row.Name;
        return View(row);
    }

    [HttpPost("/SADMIN/manufacturers/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, string? websiteUrl, string? logoUrl, string? descriptionHtml, bool isActive, CancellationToken ct)
    {
        var row = await _db.Manufacturers.FirstOrDefaultAsync(m => m.ManufacturerId == id, ct);
        if (row is null)
            return NotFound();
        row.WebsiteUrl = BlankToNull(websiteUrl);
        row.LogoUrl = BlankToNull(logoUrl);
        row.DescriptionHtml = BlankToNull(descriptionHtml);
        row.IsActive = isActive;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Saved"] = row.Name + " saved.";
        return Redirect("/SADMIN/manufacturers/" + id);
    }

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
