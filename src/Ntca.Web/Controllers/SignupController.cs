using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class SignupController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly IWebHostEnvironment _env;
    private readonly LoginCodeService _codes;
    private readonly AgentAnswerBuilder _answers;

    public SignupController(NtcaDbContext db, IAppEmailSender email, IWebHostEnvironment env, LoginCodeService codes, AgentAnswerBuilder answers)
    {
        _db = db;
        _email = email;
        _env = env;
        _codes = codes;
        _answers = answers;
    }

    [HttpGet("/signup")]
    [HttpGet("/join")]
    public Task<IActionResult> Index(string? step, CancellationToken ct)
        => Show(step ?? "who", ct);

    [HttpGet("/signup/{step}")]
    [HttpGet("/join/{step}")]
    public Task<IActionResult> StepGet(string step, CancellationToken ct)
        => Show(step, ct);

    [HttpPost("/signup/{step}")]
    [HttpPost("/join/{step}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StepPost(string step, CancellationToken ct)
    {
        var funnel = await LoadOrCreateAsync(ct);
        var answers = FunnelFlow.ReadAnswers(funnel.AnswersJson);
        MergeForm(answers, step);
        if (step == "who")
        {
            var role = AgentPrivacy.NormalizeRole(Request.Form["role"].ToString());
            if (role is null)
            {
                ViewBag.Error = "Choose who you are to continue.";
                return await Page(funnel, "who", answers, ct);
            }
            funnel.HumanRole = role;
            answers.Clear();
        }
        if (step == "email")
        {
            if (string.IsNullOrWhiteSpace(Request.Form["cf-turnstile-response"]) && !_env.IsDevelopment())
            {
                // Enforced later when a Turnstile secret is configured.
            }
            var email = (Request.Form["email"].ToString() ?? "").Trim();
            if (email.Length < 5 || !email.Contains('@'))
            {
                ViewBag.Error = "A real email is required so we can confirm the list.";
                return await Page(funnel, "email", answers, ct);
            }
            funnel.Email = email;
            funnel.FirstName = Clip(Request.Form["firstName"], 80);
            answers["sms"] = Request.Form["sms"] == "1" ? "1" : "0";
        }
        if (step == "shop_find" && Request.Form["add"] == "1")
            answers["shopAdd"] = "1";

        funnel.AnswersJson = FunnelFlow.WriteAnswers(answers);
        if (Request.Form["stay"] == "1")
        {
            funnel.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Redirect("/signup/" + step);
        }
        await RecordAsync(funnel, step, "COMPLETE", ct);
        var next = FunnelFlow.Next(funnel.HumanRole, step);
        if (next == "results")
            await CompleteAsync(funnel, answers, ct);
        else
        {
            funnel.CurrentStep = next;
            funnel.UpdatedAt = DateTime.UtcNow;
            funnel.LastStepAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await RecordAsync(funnel, next, "ENTER", ct);
        }
        return Redirect("/signup/" + next);
    }

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
                await _db.SaveChangesAsync(ct);
            }
        }
        ViewBag.Confirmed = funnel is not null;
        return View("Confirm");
    }

    [HttpGet("/signup/thanks")]
    public IActionResult Thanks() => Redirect("/signup/results");

    private async Task<IActionResult> Show(string step, CancellationToken ct)
    {
        var funnel = await LoadOrCreateAsync(ct);
        if (funnel.CurrentStep == "who" && step != "who")
            await RecordAsync(funnel, "who", "ENTER", ct);
        var answers = FunnelFlow.ReadAnswers(funnel.AnswersJson);
        if (step == "results" && funnel.Status != "COMPLETED")
            return Redirect("/signup/" + (funnel.CurrentStep.Length == 0 ? "who" : funnel.CurrentStep));
        if (funnel.CurrentStep != step && step != "results")
        {
            funnel.CurrentStep = step;
            funnel.LastStepAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await RecordAsync(funnel, step, "ENTER", ct);
        }
        return await Page(funnel, step, answers, ct);
    }

    private async Task<IActionResult> Page(SignupFunnel funnel, string step, Dictionary<string, string> answers, CancellationToken ct)
    {
        ViewBag.Funnel = funnel;
        ViewBag.Answers = answers;
        ViewBag.Step = step;
        ViewBag.Back = FunnelFlow.Back(funnel.HumanRole, step);
        ViewBag.Progress = FunnelFlow.Progress(funnel.HumanRole, step);
        ViewBag.Role = funnel.HumanRole;
        if (step is "shop_find")
        {
            var q = answers.GetValueOrDefault("shopQ") ?? "";
            var zip = answers.GetValueOrDefault("zip") ?? "";
            IQueryable<Shop> shops = _db.Shops.AsNoTracking().Include(s => s.Tier).Where(s => s.ListingStatus == "ACTIVE");
            if (!string.IsNullOrWhiteSpace(zip) || !string.IsNullOrWhiteSpace(q))
            {
                var z = zip.Trim();
                var name = q.Trim();
                shops = shops.Where(s =>
                    (z.Length > 0 && s.PostalCode != null && s.PostalCode.StartsWith(z))
                    || (name.Length > 0 && (s.Name.Contains(name) || s.City.Contains(name))));
            }
            ViewBag.Shops = await shops.OrderBy(s => s.Name).Take(12).ToListAsync(ct);
        }
        if (step == "results")
        {
            var ask = AgentPrivacy.Sanitize(default, FunnelFlow.ToAsk(funnel));
            ViewBag.Answer = await _answers.BuildAsync(ask, Origin(), ct);
            ViewBag.Tiers = await _db.ShopTiers.AsNoTracking().OrderBy(t => t.TierId).ToListAsync(ct);
        }
        return View("Step");
    }

    private async Task CompleteAsync(SignupFunnel funnel, Dictionary<string, string> answers, CancellationToken ct)
    {
        var email = (funnel.Email ?? "").Trim();
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
                FirstName = funnel.FirstName,
                HobbySegment = FunnelFlow.HobbySegment(funnel.HumanRole),
                ExtraJson = funnel.AnswersJson,
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
            signup = existing;

        if (funnel.HumanRole == "promoter")
            await MaybeSubmitShowAsync(answers, email, funnel.FirstName, ct);
        if (funnel.HumanRole == "shop_owner" && answers.TryGetValue("shopId", out var sid) && long.TryParse(sid, out var shopId))
            await _codes.IssueAsync(email, "CLAIM", null, ct);

        funnel.SignupId = signup.SignupId;
        funnel.ConfirmToken ??= Guid.NewGuid();
        funnel.Status = "COMPLETED";
        funnel.CurrentStep = "results";
        funnel.UpdatedAt = DateTime.UtcNow;
        funnel.LastStepAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await RecordAsync(funnel, "results", "ENTER", ct);
        await RecordAsync(funnel, "results", "COMPLETE", ct);

        var link = Origin() + "/signup/confirm/" + funnel.ConfirmToken;
        await _email.SendAsync(
            email,
            "Confirm your NTCA Preview signup",
            "Thanks for joining the National Trading Card Association Preview list.\n\nConfirm your email:\n" + link + "\n\nIf you did not sign up, ignore this message.\n",
            ct: ct);
    }

    private async Task MaybeSubmitShowAsync(Dictionary<string, string> answers, string email, string? name, CancellationToken ct)
    {
        var title = answers.GetValueOrDefault("showTitle");
        if (string.IsNullOrWhiteSpace(title) || !DateTime.TryParse(answers.GetValueOrDefault("showStarts"), out var starts))
            return;
        _db.EventSubmissions.Add(new EventSubmission
        {
            PublicId = Guid.NewGuid(),
            Title = title.Trim(),
            City = answers.GetValueOrDefault("showCity"),
            StateCode = string.IsNullOrWhiteSpace(answers.GetValueOrDefault("showState")) ? null : answers["showState"].Trim().ToUpperInvariant(),
            StartsAt = starts,
            SubmitterName = string.IsNullOrWhiteSpace(name) ? "Promoter" : name,
            SubmitterEmail = email,
            Status = "PENDING_REVIEW",
            Scope = "LOCAL",
            TimeZoneId = "America/New_York",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private void MergeForm(Dictionary<string, string> answers, string step)
    {
        void Set(string key, string formKey)
        {
            var v = Request.Form[formKey].ToString();
            if (!string.IsNullOrWhiteSpace(v))
                answers[key] = v.Trim();
        }
        switch (step)
        {
            case "interests":
                answers["interests"] = string.Join(",", Request.Form["interests"].Where(s => !string.IsNullOrWhiteSpace(s)));
                break;
            case "experience":
                Set("experience", "experience");
                break;
            case "zip":
                Set("zip", "zip");
                Set("city", "city");
                Set("state", "state");
                break;
            case "travel":
                Set("travel", "travel");
                break;
            case "shop_find":
                Set("shopQ", "shopQ");
                Set("zip", "zip");
                Set("shopId", "shopId");
                Set("shopName", "shopName");
                Set("shopCity", "shopCity");
                Set("shopState", "shopState");
                break;
            case "shop_tier":
                Set("tier", "tier");
                break;
            case "show":
                Set("showTitle", "showTitle");
                Set("showCity", "showCity");
                Set("showState", "showState");
                Set("showStarts", "showStarts");
                break;
            case "email":
                Set("email", "email");
                Set("firstName", "firstName");
                break;
        }
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
                CurrentStep = "who",
                Status = "IN_PROGRESS",
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
            await RecordAsync(row, "who", "ENTER", ct);
            Response.Cookies.Append(FunnelFlow.CookieName, session.ToString(), new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(14)
            });
        }
        return row;
    }

    private async Task RecordAsync(SignupFunnel funnel, string step, string action, CancellationToken ct)
    {
        _db.SignupStepEvents.Add(new SignupStepEvent
        {
            SignupFunnelId = funnel.SignupFunnelId,
            StepKey = step,
            Action = action,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private string Origin() => $"{Request.Scheme}://{Request.Host}";
}
