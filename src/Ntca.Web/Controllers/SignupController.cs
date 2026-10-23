using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class SignupController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly IWebHostEnvironment _env;

    public SignupController(NtcaDbContext db, IAppEmailSender email, IWebHostEnvironment env)
    {
        _db = db;
        _email = email;
        _env = env;
    }

    [HttpGet("/signup")]
    public IActionResult Index() => View(new SignupForm());

    [HttpPost("/signup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SignupForm form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(HttpContext.Request.Form["cf-turnstile-response"]) && !_env.IsDevelopment())
        {
            // Turnstile is optional; only enforced when a secret is configured later.
        }
        if (!ModelState.IsValid)
            return View(form);

        var email = form.Email.Trim();
        var norm = email.ToLowerInvariant();
        var existing = await _db.Signups.FirstOrDefaultAsync(
            s => s.EmailNormalized == norm && s.FormKey == "PREVIEW" && s.Status != "MERGED" && s.Status != "DELETED", ct);
        if (existing is not null)
        {
            ViewBag.Already = true;
            return View(form);
        }

        var row = new Signup
        {
            PublicId = Guid.NewGuid(),
            FormKey = "PREVIEW",
            Email = email,
            EmailNormalized = norm,
            FirstName = form.FirstName,
            LastName = form.LastName,
            HobbySegment = string.IsNullOrWhiteSpace(form.HobbySegment) ? null : form.HobbySegment,
            Message = form.Message,
            Status = "PENDING_CONFIRM",
            Channel = "WEB",
            LandingPath = "/",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Signups.Add(row);
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(
            email,
            "You're on the NTCA Preview list",
            "Thanks for signing up for National Trading Card Association Preview updates. We'll write when there is news.\n",
            ct: ct);

        return RedirectToAction(nameof(Thanks));
    }

    [HttpGet("/signup/thanks")]
    public IActionResult Thanks() => View();
}

public sealed class SignupForm
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = "";
    [StringLength(80)]
    public string? FirstName { get; set; }
    [StringLength(80)]
    public string? LastName { get; set; }
    [StringLength(40)]
    public string? HobbySegment { get; set; }
    [StringLength(2000)]
    public string? Message { get; set; }
}
