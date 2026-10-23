using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
public sealed class AccountController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly LoginCodeService _codes;

    public AccountController(NtcaDbContext db, LoginCodeService codes)
    {
        _db = db;
        _codes = codes;
    }

    [HttpGet("/SADMIN/login")]
    [HttpGet("/SADMIN")]
    public async Task<IActionResult> Login()
    {
        var ticket = await HttpContext.AuthenticateAsync(AuthSchemes.Sadmin);
        if (ticket.Succeeded)
            return Redirect("/SADMIN/dashboard");
        return View(new SadminLoginForm());
    }

    [HttpPost("/SADMIN/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(SadminLoginForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(form);
        var email = form.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email && u.AccountStatus == "ACTIVE" && u.IsStaff, ct);
        if (user is null || user.Roles.All(r => r.RoleCode is not ("SUPERADMIN" or "ADMIN" or "MODERATOR" or "EDITOR")))
        {
            ModelState.AddModelError("", "No staff account for that email.");
            return View(form);
        }
        if (string.IsNullOrWhiteSpace(form.Code))
        {
            try
            {
                var send = await _codes.IssueAsync(email, "SADMIN_LOGIN", user.UserId, ct);
                ViewBag.Sent = true;
                ViewBag.DevBypass = _codes.BypassEnabled;
                if (!send.Delivered)
                    ViewBag.MailHint = send.UserMessage;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Could not send the sign-in code: " + ex.Message);
            }
            return View(form);
        }
        if (!await _codes.VerifyAsync(email, "SADMIN_LOGIN", form.Code, ct))
        {
            ModelState.AddModelError("", "That code is invalid or expired.");
            ViewBag.Sent = true;
            ViewBag.DevBypass = _codes.BypassEnabled;
            return View(form);
        }
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuthSchemes.SignInAsync(HttpContext, AuthSchemes.Sadmin, user, user.Roles.Select(r => r.RoleCode));
        return Redirect("/SADMIN/dashboard");
    }

    [Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
    [HttpPost("/SADMIN/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Sadmin);
        return Redirect("/SADMIN/login");
    }
}

public sealed class SadminLoginForm
{
    [Required, EmailAddress]
    public string Email { get; set; } = "egarrison@wte.net";
    public string? Code { get; set; }
}
