using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "Owner", AuthenticationSchemes = AuthSchemes.Owner)]
public sealed class ShopController : Controller
{
    private readonly NtcaDbContext _db;
    public ShopController(NtcaDbContext db) => _db = db;

    [HttpGet("/owner")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var shop = await OwnedShop(userId, ct);
        if (shop is null)
            return View("NoShop");
        ViewBag.Stats7 = await ShopStatQueries.TotalsAsync(_db, shop.ShopId, 7, ct);
        ViewBag.Stats30 = await ShopStatQueries.TotalsAsync(_db, shop.ShopId, 30, ct);
        ViewBag.Stats90 = await ShopStatQueries.TotalsAsync(_db, shop.ShopId, 90, ct);
        ViewBag.Daily = await ShopStatQueries.DailyAsync(_db, shop.ShopId, 30, ct);
        return View(shop);
    }

    [HttpPost("/owner")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ShopEditForm form, CancellationToken ct)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var shop = await OwnedShop(userId, ct);
        if (shop is null)
            return View("NoShop");

        var payload = JsonSerializer.Serialize(new
        {
            form.Name,
            form.AddressLine1,
            form.City,
            form.StateCode,
            form.PostalCode,
            form.Phone,
            form.WebsiteUrl,
            form.PublicEmail,
            form.Description
        });
        _db.ShopEditRequests.Add(new ShopEditRequest
        {
            PublicId = Guid.NewGuid(),
            ShopId = shop.ShopId,
            RequestedByUserId = userId,
            ProposedJson = payload,
            Status = "PENDING_REVIEW",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        TempData["Saved"] = "Edit submitted for staff review.";
        return Redirect("/owner");
    }

    private async Task<Shop?> OwnedShop(long userId, CancellationToken ct)
    {
        var member = await _db.ShopMembers.AsNoTracking()
            .Include(m => m.Shop)
            .Where(m => m.UserId == userId && m.RevokedAt == null)
            .OrderBy(m => m.ShopMemberId)
            .FirstOrDefaultAsync(ct);
        return member?.Shop;
    }
}

public sealed class ShopEditForm
{
    public string Name { get; set; } = "";
    public string? AddressLine1 { get; set; }
    public string City { get; set; } = "";
    public string StateCode { get; set; } = "";
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? PublicEmail { get; set; }
    public string? Description { get; set; }
}
