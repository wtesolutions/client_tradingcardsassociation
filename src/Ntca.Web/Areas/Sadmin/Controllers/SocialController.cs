using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class SocialController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly ShopSocialEditor _social;

    public SocialController(NtcaDbContext db, ShopSocialEditor social)
    {
        _db = db;
        _social = social;
    }

    [HttpGet("/SADMIN/social")]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        ViewData["Title"] = "Social catalog";
        var rows = await _db.SocialPlatforms.AsNoTracking()
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Code).ToListAsync(ct);
        return View(rows);
    }

    [HttpPost("/SADMIN/social")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Catalog(string code, string label, string? iconClass, string? urlPattern, string? hostAllowlist, int displayOrder, bool isActive, string kind, CancellationToken ct)
    {
        var row = await _db.SocialPlatforms.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (row is null)
            return NotFound();
        row.Label = string.IsNullOrWhiteSpace(label) ? row.Label : label.Trim();
        row.IconClass = string.IsNullOrWhiteSpace(iconClass) ? null : iconClass.Trim();
        row.UrlPattern = string.IsNullOrWhiteSpace(urlPattern) ? null : urlPattern.Trim();
        row.HostAllowlist = string.IsNullOrWhiteSpace(hostAllowlist) ? null : hostAllowlist.Trim();
        row.DisplayOrder = displayOrder;
        row.IsActive = isActive;
        if (kind is "URL" or "EMAIL" or "PHONE")
            row.Kind = kind;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Saved"] = row.Label + " updated.";
        return Redirect("/SADMIN/social");
    }

    [HttpGet("/SADMIN/shops/{id:long}/social")]
    public async Task<IActionResult> Shop(long id, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == id, ct);
        if (shop is null)
            return NotFound();
        ViewData["Title"] = "Links · " + shop.Name;
        ViewBag.Shop = shop;
        ViewBag.Socials = await _social.LoadAsync(id, ct);
        ViewBag.Platforms = await _social.ActivePlatformsAsync(ct);
        return View();
    }

    [HttpPost("/SADMIN/shops/{id:long}/social")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(long id, string platformCode, string raw, bool verified, CancellationToken ct)
    {
        if (!await _db.Shops.AnyAsync(s => s.ShopId == id, ct))
            return NotFound();
        var error = await _social.UpsertAsync(id, platformCode, raw, "sadmin", verified, ct);
        TempData[error is null ? "Saved" : "Error"] = error ?? "Link saved.";
        return Redirect("/SADMIN/shops/" + id + "/social");
    }

    [HttpPost("/SADMIN/shops/{id:long}/social/{sid:long}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(long id, long sid, CancellationToken ct)
    {
        var error = await _social.RemoveAsync(id, sid, ct);
        TempData[error is null ? "Saved" : "Error"] = error ?? "Link removed.";
        return Redirect("/SADMIN/shops/" + id + "/social");
    }

    [HttpPost("/SADMIN/shops/{id:long}/social/{sid:long}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(long id, long sid, int delta, CancellationToken ct)
    {
        await _social.MoveAsync(id, sid, delta, ct);
        return Redirect("/SADMIN/shops/" + id + "/social");
    }
}
