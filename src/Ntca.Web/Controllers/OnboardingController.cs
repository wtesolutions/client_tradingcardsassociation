using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class OnboardingController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly AgentAnswerBuilder _answers;

    public OnboardingController(NtcaDbContext db, AgentAnswerBuilder answers)
    {
        _db = db;
        _answers = answers;
    }

    [HttpGet("/onboarding")]
    public Task<IActionResult> Index(Guid? token, CancellationToken ct)
        => Show("who", token, ct);

    [HttpGet("/onboarding/{step}")]
    public Task<IActionResult> StepGet(string step, Guid? token, CancellationToken ct)
        => Show(step, token, ct);

    [HttpPost("/onboarding/skip")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Skip(Guid? token, CancellationToken ct)
    {
        var funnel = await LoadAsync(token, ct);
        if (funnel is null)
            return Redirect("/signup");
        if (!CanOnboard(funnel))
            return Redirect("/signup/thanks");
        funnel.OnboardingStatus = FunnelFlow.OnboardSkipped;
        funnel.Status = FunnelFlow.StatusSkipped;
        funnel.Stage = FunnelFlow.StageOnboarding;
        funnel.CurrentStep = "done";
        funnel.OnboardingCompletedAt ??= DateTime.UtcNow;
        funnel.UpdatedAt = DateTime.UtcNow;
        funnel.LastStepAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await RecordAsync(funnel, "done", "COMPLETE", null, ct);
        return Redirect("/account");
    }

    [HttpPost("/onboarding/{step}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StepPost(string step, Guid? token, CancellationToken ct)
    {
        var funnel = await LoadAsync(token, ct);
        if (funnel is null)
            return Redirect("/signup");
        if (!CanOnboard(funnel))
            return Redirect("/signup/thanks");

        var answers = FunnelFlow.ReadAnswers(funnel.AnswersJson);
        var roles = FunnelFlow.ReadRoles(funnel);
        MergeForm(answers, step);

        if (step == "who")
        {
            roles = FunnelFlow.NormalizeRoles(Request.Form["roles"]);
            if (roles.Count == 0)
            {
                ViewBag.Error = "Pick at least one — you can be more than one.";
                return await Page(funnel, "who", answers, roles, ct);
            }
            funnel.RolesJson = FunnelFlow.WriteRoles(roles);
            funnel.HumanRole = roles[0];
            answers["roles"] = string.Join(",", roles);
        }

        if (step is "shop_find" or "show_find" or "mfr_find")
        {
            if (Request.Form["add"] == "1")
                await AddPendingAsync(funnel, answers, step, ct);
            else if (Request.Form["claim"] == "1" || HasSelectedTarget(answers, step))
                await ClaimPendingAsync(funnel, answers, step, ct);
        }

        funnel.AnswersJson = FunnelFlow.WriteAnswers(answers);
        MarkOnboarding(funnel, step);
        if (Request.Form["stay"] == "1")
        {
            await _db.SaveChangesAsync(ct);
            return Redirect(OnboardUrl(step, token));
        }

        await RecordAsync(funnel, step, "COMPLETE", FunnelFlow.RoleForStep(roles, step), ct);
        var next = FunnelFlow.Next(roles, step);
        if (next == "done")
        {
            funnel.OnboardingStatus = FunnelFlow.OnboardCompleted;
            funnel.Status = FunnelFlow.StatusCompleted;
            funnel.CurrentStep = "done";
            funnel.OnboardingCompletedAt ??= DateTime.UtcNow;
            await RecordAsync(funnel, "done", "ENTER", null, ct);
            await RecordAsync(funnel, "done", "COMPLETE", null, ct);
        }
        else
        {
            funnel.CurrentStep = next;
            await RecordAsync(funnel, next, "ENTER", FunnelFlow.RoleForStep(roles, next), ct);
        }
        funnel.UpdatedAt = DateTime.UtcNow;
        funnel.LastStepAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Redirect(OnboardUrl(next, token));
    }

    private async Task<IActionResult> Show(string step, Guid? token, CancellationToken ct)
    {
        var funnel = await LoadAsync(token, ct);
        if (funnel is null)
            return Redirect("/signup");
        if (!CanOnboard(funnel))
            return Redirect("/signup/thanks");

        if (funnel.OnboardingStatus == FunnelFlow.OnboardNotStarted)
        {
            funnel.OnboardingStatus = FunnelFlow.OnboardInProgress;
            funnel.Status = FunnelFlow.StatusOnboarding;
            funnel.Stage = FunnelFlow.StageOnboarding;
            funnel.OnboardingStartedAt ??= DateTime.UtcNow;
            funnel.CurrentStep = "who";
            funnel.UpdatedAt = DateTime.UtcNow;
            funnel.LastStepAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await RecordAsync(funnel, "who", "ENTER", null, ct);
            step = "who";
        }

        if (funnel.OnboardingStatus is FunnelFlow.OnboardCompleted or FunnelFlow.OnboardSkipped)
            step = "done";

        var answers = FunnelFlow.ReadAnswers(funnel.AnswersJson);
        var roles = FunnelFlow.ReadRoles(funnel);
        if (funnel.CurrentStep != step && step != "done")
        {
            funnel.CurrentStep = step;
            funnel.LastStepAt = DateTime.UtcNow;
            funnel.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await RecordAsync(funnel, step, "ENTER", FunnelFlow.RoleForStep(roles, step), ct);
        }
        return await Page(funnel, step, answers, roles, ct);
    }

    private async Task<IActionResult> Page(
        SignupFunnel funnel,
        string step,
        Dictionary<string, string> answers,
        IReadOnlyList<string> roles,
        CancellationToken ct)
    {
        ViewBag.Funnel = funnel;
        ViewBag.Answers = answers;
        ViewBag.Step = step;
        ViewBag.Roles = roles;
        ViewBag.Back = FunnelFlow.Back(roles, step);
        ViewBag.Progress = FunnelFlow.Progress(roles, step);
        ViewBag.Token = funnel.ConfirmToken;
        if (step == "shop_find")
            ViewBag.Hits = await SearchShopsAsync(answers.GetValueOrDefault("shopQ") ?? answers.GetValueOrDefault("zip"), ct);
        if (step == "show_find")
            ViewBag.Hits = await SearchShowsAsync(answers.GetValueOrDefault("showQ"), ct);
        if (step == "mfr_find")
            ViewBag.Hits = await SearchMfrsAsync(answers.GetValueOrDefault("mfrQ"), ct);
        if (step == "done")
        {
            var ask = AgentPrivacy.Sanitize(default, FunnelFlow.ToAsk(funnel));
            ViewBag.Answer = await _answers.BuildAsync(ask, Origin(), ct);
        }
        return View("Step");
    }

    private async Task<List<object>> SearchShopsAsync(string? q, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return [];
        var query = DirectorySearch.FilterShops(
            _db.Shops.AsNoTracking().Where(s => s.ListingStatus == "ACTIVE"), q!);
        var (rows, _) = await DirectorySearch.TakePageAsync(query.OrderBy(s => s.Name), 1, ct);
        return rows.Select(s => (object)new
        {
            id = s.ShopId,
            name = s.Name,
            detail = $"{s.City}, {s.StateCode}" + (string.IsNullOrWhiteSpace(s.PostalCode) ? "" : " " + s.PostalCode)
        }).ToList();
    }

    private async Task<List<object>> SearchShowsAsync(string? q, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return [];
        var query = DirectorySearch.FilterShows(
            _db.ShopEvents.AsNoTracking().Include(e => e.Venue)
                .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED")), q!);
        var (rows, _) = await DirectorySearch.TakePageAsync(query.OrderBy(e => e.StartsAt), 1, ct);
        return rows.Select(e => (object)new
        {
            id = e.ShopEventId,
            name = e.Title,
            detail = $"{e.City}, {e.StateCode} · {e.StartsAt:MMM d, yyyy}"
        }).ToList();
    }

    private async Task<List<object>> SearchMfrsAsync(string? q, CancellationToken ct)
    {
        if (!DirectorySearch.CanSearch(q))
            return [];
        var query = DirectorySearch.FilterManufacturers(
            _db.Manufacturers.AsNoTracking().Where(m => m.IsActive), q!);
        var (rows, _) = await DirectorySearch.TakePageAsync(query.OrderBy(m => m.Name), 1, ct);
        return rows.Select(m => (object)new
        {
            id = m.ManufacturerId,
            name = m.Name,
            detail = m.CountryName ?? ""
        }).ToList();
    }

    private async Task AddPendingAsync(SignupFunnel funnel, Dictionary<string, string> answers, string step, CancellationToken ct)
    {
        var (kind, name, city, state, zip, extra) = step switch
        {
            "shop_find" => ("SHOP", answers.GetValueOrDefault("shopName"), answers.GetValueOrDefault("shopCity"),
                answers.GetValueOrDefault("shopState"), answers.GetValueOrDefault("shopZip") ?? answers.GetValueOrDefault("zip"),
                ShopSocialExtra(answers)),
            "show_find" => ("SHOW", answers.GetValueOrDefault("showTitle"), answers.GetValueOrDefault("showCity"),
                answers.GetValueOrDefault("showState"), answers.GetValueOrDefault("showZip"),
                new Dictionary<string, string> { ["starts"] = answers.GetValueOrDefault("showStarts") ?? "" }),
            "mfr_find" => ("MANUFACTURER", answers.GetValueOrDefault("mfrName"), null, null, null, null),
            _ => ("SHOP", null, null, null, null, null)
        };
        if (string.IsNullOrWhiteSpace(name))
            return;
        answers[step + "Add"] = "1";
        await WriteSubmissionAsync(funnel, kind, "ADD", null, name!, city, state, zip, extra, ct);
        if (kind == "SHOW" && DateTime.TryParse(answers.GetValueOrDefault("showStarts"), out var starts))
        {
            _db.EventSubmissions.Add(new EventSubmission
            {
                PublicId = Guid.NewGuid(),
                Title = name!.Trim(),
                City = city,
                StateCode = string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant(),
                PostalCode = zip,
                StartsAt = starts,
                SubmitterName = funnel.FirstName ?? "Promoter",
                SubmitterEmail = funnel.Email ?? "",
                Status = "PENDING_REVIEW",
                Scope = "LOCAL",
                TimeZoneId = "America/New_York",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task ClaimPendingAsync(SignupFunnel funnel, Dictionary<string, string> answers, string step, CancellationToken ct)
    {
        var (kind, idKey, nameKey) = step switch
        {
            "shop_find" => ("SHOP", "shopId", "shopName"),
            "show_find" => ("SHOW", "showId", "showTitle"),
            "mfr_find" => ("MANUFACTURER", "mfrId", "mfrName"),
            _ => ("SHOP", "shopId", "shopName")
        };
        if (!answers.TryGetValue(idKey, out var raw) || !long.TryParse(raw, out var id))
            return;
        answers[step + "Claim"] = raw;
        await WriteSubmissionAsync(funnel, kind, "CLAIM", id, answers.GetValueOrDefault(nameKey) ?? kind, null, null, null, null, ct);
    }

    private async Task WriteSubmissionAsync(
        SignupFunnel funnel, string kind, string mode, long? targetId,
        string name, string? city, string? state, string? zip,
        Dictionary<string, string>? extra, CancellationToken ct)
    {
        var existing = await _db.OnboardingSubmissions.FirstOrDefaultAsync(s =>
            s.SignupFunnelId == funnel.SignupFunnelId
            && s.Kind == kind
            && s.Mode == mode
            && s.Status == "PENDING_REVIEW"
            && (targetId == null ? s.Name == name : s.TargetId == targetId), ct);
        if (existing is not null)
            return;
        _db.OnboardingSubmissions.Add(new OnboardingSubmission
        {
            PublicId = Guid.NewGuid(),
            SignupFunnelId = funnel.SignupFunnelId,
            SignupId = funnel.SignupId,
            Kind = kind,
            Mode = mode,
            TargetId = targetId,
            Name = name.Trim(),
            City = Clip(city, 100),
            StateCode = string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant(),
            PostalCode = Clip(zip, 20),
            ExtraJson = extra is null ? null : JsonSerializer.Serialize(extra),
            SubmitterName = funnel.FirstName ?? "",
            SubmitterEmail = funnel.Email ?? "",
            Status = "PENDING_REVIEW",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static bool HasSelectedTarget(Dictionary<string, string> answers, string step)
        => step switch
        {
            "shop_find" => answers.ContainsKey("shopId"),
            "show_find" => answers.ContainsKey("showId"),
            "mfr_find" => answers.ContainsKey("mfrId"),
            _ => false
        };

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
                Set("shopZip", "shopZip");
                Set("shopWebsite", "shopWebsite");
                Set("shopFacebook", "shopFacebook");
                Set("shopInstagram", "shopInstagram");
                Set("shopTiktok", "shopTiktok");
                Set("shopYoutube", "shopYoutube");
                Set("shopWhatnot", "shopWhatnot");
                break;
            case "shop_tier":
                Set("tier", "tier");
                break;
            case "show_find":
                Set("showQ", "showQ");
                Set("showId", "showId");
                Set("showTitle", "showTitle");
                Set("showCity", "showCity");
                Set("showState", "showState");
                Set("showZip", "showZip");
                Set("showStarts", "showStarts");
                break;
            case "mfr_find":
                Set("mfrQ", "mfrQ");
                Set("mfrId", "mfrId");
                Set("mfrName", "mfrName");
                break;
        }
    }

    private async Task<SignupFunnel?> LoadAsync(Guid? token, CancellationToken ct)
    {
        SignupFunnel? row = null;
        if (token is Guid t && t != Guid.Empty)
            row = await _db.SignupFunnels.FirstOrDefaultAsync(f => f.ConfirmToken == t, ct);
        if (row is null && Request.Cookies.TryGetValue(FunnelFlow.CookieName, out var raw) && Guid.TryParse(raw, out var session))
            row = await _db.SignupFunnels.FirstOrDefaultAsync(f => f.SessionId == session, ct);
        if (row is not null)
            Response.Cookies.Append(FunnelFlow.CookieName, row.SessionId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        return row;
    }

    private async Task RecordAsync(SignupFunnel funnel, string step, string action, string? role, CancellationToken ct)
    {
        _db.SignupStepEvents.Add(new SignupStepEvent
        {
            SignupFunnelId = funnel.SignupFunnelId,
            StepKey = step,
            Action = action,
            Stage = FunnelFlow.StageOnboarding,
            RoleKey = role,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static void MarkOnboarding(SignupFunnel funnel, string step)
    {
        funnel.Stage = FunnelFlow.StageOnboarding;
        if (funnel.OnboardingStatus == FunnelFlow.OnboardNotStarted)
        {
            funnel.OnboardingStatus = FunnelFlow.OnboardInProgress;
            funnel.OnboardingStartedAt ??= DateTime.UtcNow;
        }
        if (funnel.Status is FunnelFlow.StatusConfirmed or FunnelFlow.StatusRegistered)
            funnel.Status = FunnelFlow.StatusOnboarding;
        funnel.CurrentStep = step;
        funnel.UpdatedAt = DateTime.UtcNow;
        funnel.LastStepAt = DateTime.UtcNow;
    }

    private static bool CanOnboard(SignupFunnel funnel)
        => funnel.SignupId is not null
           && (funnel.ConfirmedAt is not null
               || funnel.Status is FunnelFlow.StatusConfirmed
                   or FunnelFlow.StatusOnboarding
                   or FunnelFlow.StatusCompleted
                   or FunnelFlow.StatusSkipped);

    private static string OnboardUrl(string step, Guid? token)
        => token is Guid t && t != Guid.Empty
            ? $"/onboarding/{step}?token={t}"
            : "/onboarding/" + step;

    private static Dictionary<string, string>? ShopSocialExtra(Dictionary<string, string> answers)
    {
        var extra = new Dictionary<string, string>();
        void Take(string key, string platform)
        {
            if (!answers.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                return;
            if (ShopSocialNormalizer.TryNormalize(platform, raw, out var url, out var handle, out _))
            {
                extra[platform] = url;
                if (!string.IsNullOrWhiteSpace(handle))
                    extra[platform + "_handle"] = handle!;
            }
            else
                extra[platform] = raw.Trim();
        }
        Take("shopWebsite", ShopSocialCatalog.Website);
        Take("shopFacebook", ShopSocialCatalog.Facebook);
        Take("shopInstagram", ShopSocialCatalog.Instagram);
        Take("shopTiktok", ShopSocialCatalog.TikTok);
        Take("shopYoutube", ShopSocialCatalog.YouTube);
        Take("shopWhatnot", ShopSocialCatalog.Whatnot);
        return extra.Count == 0 ? null : extra;
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
