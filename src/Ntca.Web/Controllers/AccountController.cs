using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class AccountController : Controller
{
    private readonly NtcaDbContext _db;
    public AccountController(NtcaDbContext db) => _db = db;

    [HttpGet("/account")]
    public async Task<IActionResult> Index(Guid? token, CancellationToken ct)
    {
        Ntca.Core.SignupFunnel? funnel = null;
        if (token is Guid t && t != Guid.Empty)
            funnel = await _db.SignupFunnels.FirstOrDefaultAsync(f => f.ConfirmToken == t, ct);
        if (funnel is null && Request.Cookies.TryGetValue(FunnelFlow.CookieName, out var raw) && Guid.TryParse(raw, out var session))
            funnel = await _db.SignupFunnels.FirstOrDefaultAsync(f => f.SessionId == session, ct);
        if (funnel is null || funnel.SignupId is null)
            return Redirect("/signup");

        var signup = await _db.Signups.AsNoTracking().FirstOrDefaultAsync(s => s.SignupId == funnel.SignupId, ct);
        ViewBag.Funnel = funnel;
        ViewBag.Signup = signup;
        ViewBag.Roles = FunnelFlow.ReadRoles(funnel);
        ViewBag.Token = funnel.ConfirmToken;
        return View();
    }
}
