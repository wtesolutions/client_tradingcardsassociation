using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Owner.Controllers;

[Area("Owner")]
public sealed class AccountController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly LoginCodeService _codes;

    public AccountController(NtcaDbContext db, LoginCodeService codes)
    {
        _db = db;
        _codes = codes;
    }

    [HttpGet("/owner/login")]
    public IActionResult Login() => View(new OwnerLoginForm());

    [HttpPost("/owner/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(OwnerLoginForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(form);
        var email = form.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Roles).Include(u => u.ShopMemberships)
            .FirstOrDefaultAsync(u => u.Email == email && u.AccountStatus == "ACTIVE", ct);
        if (user is null)
        {
            ModelState.AddModelError("", "No owner account for that email.");
            return View(form);
        }
        if (string.IsNullOrWhiteSpace(form.Code))
        {
            await _codes.IssueAsync(email, "LOGIN", user.UserId, ct);
            ViewBag.Sent = true;
            return View(form);
        }
        if (!await _codes.VerifyAsync(email, "LOGIN", form.Code, ct))
        {
            ModelState.AddModelError("", "That code is invalid or expired.");
            ViewBag.Sent = true;
            return View(form);
        }
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuthSchemes.SignInAsync(HttpContext, AuthSchemes.Owner, user, user.Roles.Select(r => r.RoleCode));
        return Redirect("/owner");
    }

    [Authorize(Policy = "Owner", AuthenticationSchemes = AuthSchemes.Owner)]
    [HttpPost("/owner/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Owner);
        return Redirect("/owner/login");
    }
}

public sealed class OwnerLoginForm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
    public string? Code { get; set; }
}
