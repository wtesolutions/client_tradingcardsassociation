using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
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
    public async Task<IActionResult> Index(string? q, string? missing, int? page, int? pageSize, long? inspect, CancellationToken ct)
    {
        var size = pageSize is 50 or 100 ? pageSize.Value : 25;
        var p = page is > 0 ? page.Value : 1;
        var query = _db.Manufacturers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m => m.Name.Contains(term) || m.Slug.Contains(term) || m.NormalizedName.Contains(term));
        }
        if (string.Equals(missing, "logo", StringComparison.OrdinalIgnoreCase))
            query = query.Where(m => m.LogoUrl == null || m.LogoUrl == "");
        else if (string.Equals(missing, "description", StringComparison.OrdinalIgnoreCase))
            query = query.Where(m => m.DescriptionHtml == null || m.DescriptionHtml == "");
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(m => m.Name).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        Manufacturer? inspectRow = null;
        if (inspect is long id)
            inspectRow = rows.FirstOrDefault(m => m.ManufacturerId == id)
                ?? await _db.Manufacturers.AsNoTracking().FirstOrDefaultAsync(m => m.ManufacturerId == id, ct);
        ViewData["Title"] = "Manufacturers";
        return View(new SadminManufacturerListVm
        {
            Rows = rows,
            Inspect = inspectRow,
            Q = q ?? "",
            Missing = missing ?? "",
            Page = p,
            PageSize = size,
            Total = total
        });
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

public sealed class SadminManufacturerListVm
{
    public List<Manufacturer> Rows { get; set; } = [];
    public Manufacturer? Inspect { get; set; }
    public string Q { get; set; } = "";
    public string Missing { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int Total { get; set; }
}
