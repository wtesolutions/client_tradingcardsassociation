using System.Security.Claims;
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
    private readonly ShopSocialEditor _social;
    private readonly OwnerShopWork _work;
    private readonly ShopMediaService _media;
    private readonly IFileStorage _files;

    public ShopController(NtcaDbContext db, ShopSocialEditor social, OwnerShopWork work, ShopMediaService media, IFileStorage files)
    {
        _db = db;
        _social = social;
        _work = work;
        _media = media;
        _files = files;
    }

    [HttpGet("/owner")]
    public async Task<IActionResult> Index(long? shop, CancellationToken ct)
    {
        var userId = UserId();
        var shops = await _work.OwnedShopsAsync(userId, ct);
        var current = await _work.OwnedAsync(userId, shop, ct);
        if (current is null)
            return View("NoShop");
        ViewBag.OwnedShops = shops;
        ViewBag.Stats7 = await ShopStatQueries.TotalsAsync(_db, current.ShopId, 7, ct);
        ViewBag.Stats30 = await ShopStatQueries.TotalsAsync(_db, current.ShopId, 30, ct);
        ViewBag.Stats90 = await ShopStatQueries.TotalsAsync(_db, current.ShopId, 90, ct);
        ViewBag.Daily = await ShopStatQueries.DailyAsync(_db, current.ShopId, 30, ct);
        ViewBag.Socials = await _social.LoadAsync(current.ShopId, ct);
        ViewBag.Platforms = await _social.ActivePlatformsAsync(ct);
        ViewBag.Photos = await _media.ListAsync(current.ShopId, ct);
        ViewBag.LogoUrl = ShopMediaService.LogoUrl(_files, current);
        ViewBag.Files = _files;
        return View(current);
    }

    [HttpPost("/owner/identity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Identity(long? shopId, ShopEditForm form, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        await _work.SubmitIdentityAsync(shop, userId, form, ct);
        TempData["Saved"] = "Name and address submitted for staff review.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/live")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Live(long? shopId, ShopEditForm form, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        await _work.PublishImmediateAsync(shop, userId, form, HoursFromForm(), ct);
        TempData["Saved"] = "Phone, website, hours, and description are live.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Index(long? shopId, ShopEditForm form, CancellationToken ct)
        => Identity(shopId, form, ct);

    [HttpPost("/owner/claim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(long shopId, CancellationToken ct)
    {
        var userId = UserId();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        TempData["Saved"] = await _work.SubmitClaimAsync(userId, email, shopId, ct);
        return Redirect("/owner");
    }

    [HttpPost("/owner/logo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logo(long shopId, IFormFile file, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        var err = await _media.SaveLogoAsync(shop, userId, file, ct);
        TempData[err is null ? "Saved" : "MediaError"] = err ?? "Logo published.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/photos")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Photos(long shopId, IFormFile file, string? alt, string? caption, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        var err = await _media.SavePhotoAsync(shop, userId, file, alt, caption, ct);
        TempData[err is null ? "Saved" : "MediaError"] = err ?? "Photo published.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/photos/{id:long}/cover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cover(long id, long shopId, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        await _media.SetCoverAsync(shop, id, userId, ct);
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/photos/{id:long}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MovePhoto(long id, long shopId, int delta, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        await _media.ReorderAsync(shop, id, delta, userId, ct);
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/photos/{id:long}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(long id, long shopId, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        await _media.DeletePhotoAsync(shop, id, userId, ct);
        TempData["Saved"] = "Photo removed.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/social")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSocial(long? shopId, string platformCode, string raw, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        var error = await _social.UpsertAsync(shop.ShopId, platformCode, raw, "owner", verified: false, ct);
        TempData[error is null ? "Saved" : "SocialError"] = error ?? "Social link saved. It is live now.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/social/{id:long}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSocial(long id, long? shopId, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        var error = await _social.RemoveAsync(shop.ShopId, id, ct);
        TempData[error is null ? "Saved" : "SocialError"] = error ?? "Social link removed.";
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    [HttpPost("/owner/social/{id:long}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveSocial(long id, long? shopId, int delta, CancellationToken ct)
    {
        var userId = UserId();
        var shop = await _work.OwnedAsync(userId, shopId, ct);
        if (shop is null)
            return View("NoShop");
        await _social.MoveAsync(shop.ShopId, id, delta, ct);
        return Redirect("/owner?shop=" + shop.ShopId);
    }

    private long UserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private IReadOnlyList<HourRow> HoursFromForm()
    {
        var hours = new List<HourRow>();
        for (byte d = 0; d <= 6; d++)
        {
            var closed = string.Equals(Request.Form[$"hours[{d}].Closed"], "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Request.Form[$"hours[{d}].Closed"], "on", StringComparison.OrdinalIgnoreCase);
            hours.Add(new HourRow
            {
                Day = d,
                Closed = closed,
                Opens = Request.Form[$"hours[{d}].Opens"],
                Closes = Request.Form[$"hours[{d}].Closes"]
            });
        }
        return hours;
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
    public string? PrimarySpecialty { get; set; }
}
