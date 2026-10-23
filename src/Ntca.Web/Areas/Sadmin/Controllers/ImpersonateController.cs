using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Impersonate", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class ImpersonateController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly ImpersonationService _imp;

    public ImpersonateController(NtcaDbContext db, ImpersonationService imp)
    {
        _db = db;
        _imp = imp;
    }

    [HttpGet("/SADMIN/impersonate/{shopId:long}")]
    public async Task<IActionResult> Confirm(long shopId, string? email, CancellationToken ct)
    {
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == shopId, ct);
        if (shop is null)
            return NotFound();
        var owner = await _db.ShopMembers.AsNoTracking().Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ShopId == shopId && m.RevokedAt == null && m.MemberRole == "OWNER", ct);
        ViewBag.Email = email ?? "";
        ViewBag.Owner = owner?.User;
        return View(shop);
    }

    [HttpPost("/SADMIN/impersonate/{shopId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(long shopId, string? email, string? confirm, CancellationToken ct)
    {
        if (!string.Equals(confirm, "yes", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ImpersonateError"] = "Confirm impersonation before continuing.";
            return Redirect($"/SADMIN/impersonate/{shopId}?email={Uri.EscapeDataString(email ?? "")}");
        }
        var error = await _imp.StartAsync(User, shopId, email, ct);
        if (error is not null)
        {
            TempData["ImpersonateError"] = error;
            return Redirect($"/SADMIN/impersonate/{shopId}?email={Uri.EscapeDataString(email ?? "")}");
        }
        return Redirect("/owner?shop=" + shopId);
    }

    [HttpPost("/SADMIN/impersonate/stop")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        await _imp.StopAsync(ct);
        return LocalRedirect("/SADMIN/shops");
    }
}
