using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class SignupController : Controller
{
    private static readonly HashSet<string> OnboardingSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        "who", "zip", "interests", "experience", "travel",
        "shop_find", "shop_tier", "show", "show_find", "mfr_find", "done", "results"
    };

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
    [HttpGet("/join")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var funnel = await LoadOrCreateAsync(ct);
        if (funnel.Status is FunnelFlow.StatusRegistered)
            return Redirect("/signup/thanks");
        if (IsOnboardable(funnel))
            return Redirect("/onboarding");
        await RecordAsync(funnel, "register", "ENTER", FunnelFlow.StageRegister, null, ct);
        return View("Index");
    }

    [HttpGet("/signup/{step}")]
    [HttpGet("/join/{step}")]
    public IActionResult LegacyStep(string step)
    {
        if (string.Equals(step, "thanks", StringComparison.OrdinalIgnoreCase))
            return Redirect("/signup/thanks");
        if (string.Equals(step, "register", StringComparison.OrdinalIgnoreCase))
            return Redirect("/signup");
        if (OnboardingSteps.Contains(step))
            return Redirect("/onboarding/" + step.ToLowerInvariant());
        return Redirect("/signup");
    }

    [HttpPost("/signup")]
    [HttpPost("/join")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(CancellationToken ct)
    {
        var funnel = await LoadOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(Request.Form["cf-turnstile-response"]) && !_env.IsDevelopment())
        {
            // Enforced later when a Turnstile secret is configured.
        }

        var name = Clip(Request.Form["name"], 160) ?? "";
        var email = (Request.Form["email"].ToString() ?? "").Trim();
        if (email.Length < 5 || !email.Contains('@'))
        {
            ViewBag.Error = "A real email is required so we can confirm the list.";
            ViewBag.Name = name;
            ViewBag.Email = email;
            ViewBag.Zip = Request.Form["zip"].ToString();
            ViewBag.Phone = Request.Form["phone"].ToString();
            ViewBag.Social = Request.Form["social"].ToString();
            return View("Index");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            ViewBag.Error = "Name is required.";
            ViewBag.Email = email;
            ViewBag.Zip = Request.Form["zip"].ToString();
            ViewBag.Phone = Request.Form["phone"].ToString();
            ViewBag.Social = Request.Form["social"].ToString();
            return View("Index");
        }

        var (first, last) = FunnelFlow.SplitName(name);
        var zip = Clip(Request.Form["zip"], 10);
        var phone = Clip(Request.Form["phone"], 25);
        var social = Clip(Request.Form["social"], 120);
        var answers = FunnelFlow.ReadAnswers(funnel.AnswersJson);
        if (!string.IsNullOrWhiteSpace(zip))
            answers["zip"] = zip;
        if (!string.IsNullOrWhiteSpace(phone))
            answers["phone"] = phone;
        if (!string.IsNullOrWhiteSpace(social))
            answers["social"] = social;
        answers["name"] = name;

        var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(zip))
            extra["zip"] = zip;
        if (!string.IsNullOrWhiteSpace(social))
            extra["social"] = social;

        var norm = email.ToLowerInvariant();
        var existing = await _db.Signups.FirstOrDefaultAsync(
            s => s.EmailNormalized == norm && s.FormKey == "PREVIEW" && s.Status != "MERGED" && s.Status != "DELETED", ct);
        Signup signup;
        if (existing is null)
        {
            signup = new Signup
            {
                PublicId = Guid.NewGuid(),
                FormKey = "PREVIEW",
                Email = email,
                EmailNormalized = norm,
                FirstName = first,
                LastName = last,
                Phone = phone,
                SocialHandle = social,
                ExtraJson = extra.Count == 0 ? null : JsonSerializer.Serialize(extra),
                Status = "PENDING_CONFIRM",
                Channel = "WEB",
                UtmSource = funnel.UtmSource,
                UtmMedium = funnel.UtmMedium,
                UtmCampaign = funnel.UtmCampaign,
                LandingPath = funnel.LandingPath ?? "/signup",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Signups.Add(signup);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            signup = existing;
            signup.FirstName ??= first;
            signup.LastName ??= last;
            signup.Phone ??= phone;
            signup.SocialHandle ??= social;
            signup.UpdatedAt = DateTime.UtcNow;
        }

        funnel.Email = email;
        funnel.FirstName = first;
        funnel.SignupId = signup.SignupId;
        funnel.ConfirmToken ??= Guid.NewGuid();
        funnel.AnswersJson = FunnelFlow.WriteAnswers(answers);
        funnel.Status = FunnelFlow.StatusRegistered;
        funnel.Stage = FunnelFlow.StageRegister;
        funnel.CurrentStep = "thanks";
        funnel.RegisteredAt ??= DateTime.UtcNow;
        funnel.UpdatedAt = DateTime.UtcNow;
        funnel.LastStepAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await RecordAsync(funnel, "register", "COMPLETE", FunnelFlow.StageRegister, null, ct);
        await RecordAsync(funnel, "thanks", "ENTER", FunnelFlow.StageRegister, null, ct);

        var link = Origin() + "/signup/confirm/" + funnel.ConfirmToken;
        await _email.SendAsync(
            email,
            "Confirm your NTCA Preview signup",
            "Thanks for joining the National Trading Card Association Preview list.\n\nConfirm your email:\n" + link + "\n\nIf you did not sign up, ignore this message.\n",
            ct: ct);

        return Redirect("/signup/thanks");
    }

    [HttpGet("/signup/thanks")]
    public IActionResult Thanks() => View("Thanks");

    [HttpGet("/signup/confirm/{token:guid}")]
    public async Task<IActionResult> Confirm(Guid token, CancellationToken ct)
    {
        var funnel = await _db.SignupFunnels.FirstOrDefaultAsync(f => f.ConfirmToken == token, ct);
        if (funnel?.SignupId is long id)
        {
            var signup = await _db.Signups.FirstOrDefaultAsync(s => s.SignupId == id, ct);
            if (signup is not null && signup.Status == "PENDING_CONFIRM")
            {
                signup.Status = "CONFIRMED";
                signup.ConfirmedAt = DateTime.UtcNow;
                signup.UpdatedAt = DateTime.UtcNow;
            }
            funnel.Status = funnel.OnboardingStatus == FunnelFlow.OnboardCompleted
                ? FunnelFlow.StatusCompleted
                : FunnelFlow.StatusConfirmed;
            funnel.Stage = FunnelFlow.StageOnboarding;
            funnel.ConfirmedAt ??= DateTime.UtcNow;
            funnel.UpdatedAt = DateTime.UtcNow;
            funnel.LastStepAt = DateTime.UtcNow;
            if (funnel.OnboardingStatus == FunnelFlow.OnboardNotStarted)
                funnel.CurrentStep = "who";
            await _db.SaveChangesAsync(ct);
            await RecordAsync(funnel, "confirm", "COMPLETE", FunnelFlow.StageRegister, null, ct);
            Response.Cookies.Append(FunnelFlow.CookieName, funnel.SessionId.ToString(), Cookie());
        }
        ViewBag.Confirmed = funnel is not null;
        ViewBag.OnboardingDone = funnel?.OnboardingStatus is FunnelFlow.OnboardCompleted or FunnelFlow.OnboardSkipped;
        ViewBag.Token = token;
        return View("Confirm");
    }

    private async Task<SignupFunnel> LoadOrCreateAsync(CancellationToken ct)
    {
        Guid session = Guid.Empty;
        if (Request.Cookies.TryGetValue(FunnelFlow.CookieName, out var raw))
            Guid.TryParse(raw, out session);
        SignupFunnel? row = null;
        if (session != Guid.Empty)
            row = await _db.SignupFunnels.FirstOrDefaultAsync(f => f.SessionId == session, ct);
        if (row is null)
        {
            session = Guid.NewGuid();
            row = new SignupFunnel
            {
                SessionId = session,
                CurrentStep = "register",
                Stage = FunnelFlow.StageRegister,
                Status = FunnelFlow.StatusInProgress,
                OnboardingStatus = FunnelFlow.OnboardNotStarted,
                UtmSource = Request.Query["utm_source"].ToString(),
                UtmMedium = Request.Query["utm_medium"].ToString(),
                UtmCampaign = Request.Query["utm_campaign"].ToString(),
                LandingPath = Request.Path.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastStepAt = DateTime.UtcNow
            };
            _db.SignupFunnels.Add(row);
            await _db.SaveChangesAsync(ct);
            Response.Cookies.Append(FunnelFlow.CookieName, session.ToString(), Cookie());
        }
        return row;
    }

    private async Task RecordAsync(SignupFunnel funnel, string step, string action, string stage, string? role, CancellationToken ct)
    {
        _db.SignupStepEvents.Add(new SignupStepEvent
        {
            SignupFunnelId = funnel.SignupFunnelId,
            StepKey = step,
            Action = action,
            Stage = stage,
            RoleKey = role,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private CookieOptions Cookie() => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = Request.IsHttps,
        Expires = DateTimeOffset.UtcNow.AddDays(30)
    };

    private static bool IsOnboardable(SignupFunnel funnel)
        => funnel.Status is FunnelFlow.StatusConfirmed or FunnelFlow.StatusOnboarding or FunnelFlow.StatusCompleted
            || funnel.OnboardingStatus is FunnelFlow.OnboardInProgress or FunnelFlow.OnboardCompleted;

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private string Origin() => $"{Request.Scheme}://{Request.Host}";
}
