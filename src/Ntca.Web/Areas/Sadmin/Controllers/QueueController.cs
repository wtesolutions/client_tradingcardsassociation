using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Areas.Owner.Controllers;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class QueueController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly OwnerShopWork _owner;
    private readonly ShopMediaService _media;
    public QueueController(NtcaDbContext db, OwnerShopWork owner, ShopMediaService media)
    {
        _db = db;
        _owner = owner;
        _media = media;
    }

    [HttpGet("/SADMIN/queue/events")]
    public async Task<IActionResult> Events(CancellationToken ct)
    {
        var rows = await _db.EventSubmissions.AsNoTracking()
            .Where(s => s.Status == "PENDING_REVIEW")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
        return View(rows);
    }

    [HttpPost("/SADMIN/queue/events/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideEvent(long id, string decision, string? reason, CancellationToken ct)
    {
        var row = await _db.EventSubmissions.FirstOrDefaultAsync(s => s.EventSubmissionId == id, ct);
        if (row is null)
            return NotFound();
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        row.ReviewedByUserId = userId;
        row.ReviewedAt = DateTime.UtcNow;
        row.DecisionReason = reason;
        row.UpdatedAt = DateTime.UtcNow;
        if (decision == "approve")
        {
            row.Status = "APPROVED";
            var ev = new ShopEvent
            {
                PublicId = Guid.NewGuid(),
                ShopId = row.ShopId,
                PromoterId = row.PromoterId,
                Title = row.Title,
                Slug = Slugify(row.Title + "-" + row.StartsAt.ToString("yyyy-MM")),
                Description = row.Description,
                EventType = "CARD_SHOW",
                StartsAt = row.StartsAt,
                EndsAt = row.EndsAt,
                TimeZoneId = row.TimeZoneId,
                Status = "PUBLISHED",
                SourceType = "SUBMIT",
                Scope = row.Scope,
                TableCount = row.TableCount,
                AdmissionText = row.AdmissionText,
                HoursText = row.HoursText,
                FocusTags = row.FocusTags,
                OrganizerUrl = row.OrganizerUrl,
                StateCode = row.StateCode,
                City = row.City,
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (ev.ShopId is null && ev.PromoterId is null)
            {
                var slug = Slugify(row.PromoterName ?? row.SubmitterName);
                var promoter = await _db.Promoters.FirstOrDefaultAsync(p => p.Slug == slug, ct);
                if (promoter is null)
                {
                    promoter = new Promoter
                    {
                        PublicId = Guid.NewGuid(),
                        Slug = slug,
                        Name = row.PromoterName ?? row.SubmitterName
                    };
                    _db.Promoters.Add(promoter);
                    await _db.SaveChangesAsync(ct);
                }
                ev.PromoterId = promoter.PromoterId;
            }
            _db.ShopEvents.Add(ev);
            await _db.SaveChangesAsync(ct);
            row.ShopEventId = ev.ShopEventId;
        }
        else
        {
            row.Status = "REJECTED";
        }
        await _db.SaveChangesAsync(ct);
        return Redirect("/SADMIN/queue/events");
    }

    [HttpGet("/SADMIN/queue/edits")]
    public async Task<IActionResult> Edits(CancellationToken ct)
    {
        var pending = await _db.ShopEditRequests.AsNoTracking()
            .Include(e => e.Shop)
            .Where(s => s.Status == "PENDING_REVIEW")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
        var fyi = await _db.ShopEditRequests.AsNoTracking()
            .Include(e => e.Shop)
            .Where(s => s.Status == "FYI")
            .OrderByDescending(s => s.CreatedAt)
            .Take(40)
            .ToListAsync(ct);
        return View((pending, fyi));
    }

    [HttpPost("/SADMIN/queue/edits/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideEdit(long id, string decision, string? reason, CancellationToken ct)
    {
        var row = await _db.ShopEditRequests.Include(e => e.Shop).FirstOrDefaultAsync(s => s.ShopEditRequestId == id, ct);
        if (row is null)
            return NotFound();
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        row.ReviewedByUserId = userId;
        row.ReviewedAt = DateTime.UtcNow;
        row.DecisionReason = reason;
        row.UpdatedAt = DateTime.UtcNow;
        if (decision == "undo")
        {
            await _media.UndoFyiAsync(row, ct);
            return Redirect("/SADMIN/queue/edits");
        }
        if (decision == "approve" && row.Shop is not null)
        {
            row.Status = "APPROVED";
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.ProposedJson) ? "{}" : row.ProposedJson);
            var r = doc.RootElement;
            var kind = r.TryGetProperty("kind", out var k) ? k.GetString() : null;
            if (string.Equals(kind, "identity", StringComparison.OrdinalIgnoreCase))
                await _owner.ApplyApprovedIdentityAsync(row.Shop, r);
            else
            {
                row.Shop.Name = Str(r, "Name") ?? row.Shop.Name;
                row.Shop.AddressLine1 = Str(r, "AddressLine1") ?? row.Shop.AddressLine1;
                row.Shop.City = Str(r, "City") ?? row.Shop.City;
                row.Shop.StateCode = (Str(r, "StateCode") ?? row.Shop.StateCode).ToUpperInvariant();
                row.Shop.PostalCode = Str(r, "PostalCode") ?? row.Shop.PostalCode;
                row.Shop.Phone = Str(r, "Phone") ?? row.Shop.Phone;
                row.Shop.WebsiteUrl = Str(r, "WebsiteUrl") ?? row.Shop.WebsiteUrl;
                row.Shop.PublicEmail = Str(r, "PublicEmail") ?? row.Shop.PublicEmail;
                row.Shop.Description = Str(r, "Description") ?? row.Shop.Description;
                row.Shop.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            row.Status = "REJECTED";
        }
        await _db.SaveChangesAsync(ct);
        return Redirect("/SADMIN/queue/edits");
    }

    [HttpGet("/SADMIN/queue/claims")]
    public async Task<IActionResult> Claims(CancellationToken ct)
    {
        var rows = await _db.ShopClaimRequests.AsNoTracking()
            .Include(c => c.Shop)
            .Include(c => c.RequestedBy)
            .Where(c => c.Status == "PENDING" || c.Status == "VERIFYING")
            .OrderBy(c => c.SubmittedAt)
            .ToListAsync(ct);
        return View(rows);
    }

    [HttpPost("/SADMIN/queue/claims/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideClaim(long id, string decision, string? reason, CancellationToken ct)
    {
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _owner.DecideClaimAsync(id, userId, decision == "approve", reason, ct);
        return Redirect("/SADMIN/queue/claims");
    }

    [HttpGet("/SADMIN/queue/drafts")]
    public async Task<IActionResult> Drafts(CancellationToken ct)
    {
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => p.Status == "DRAFT")
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
        var figures = await _db.IndustryFigures.AsNoTracking()
            .Include(f => f.Sources)
            .Where(f => !f.IsPublished)
            .OrderBy(f => f.FullName)
            .ToListAsync(ct);
        return View((posts, figures));
    }

    [HttpPost("/SADMIN/queue/drafts/post/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishPost(long id, string decision, CancellationToken ct)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.PostId == id, ct);
        if (post is null)
            return NotFound();
        if (decision == "publish")
        {
            post.Status = "PUBLISHED";
            post.PublishedAt ??= DateTime.UtcNow;
        }
        else if (decision == "archive")
        {
            post.Status = "ARCHIVED";
        }
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Redirect("/SADMIN/queue/drafts");
    }

    [HttpPost("/SADMIN/queue/drafts/figure/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishFigure(long id, string decision, CancellationToken ct)
    {
        var fig = await _db.IndustryFigures.FirstOrDefaultAsync(f => f.IndustryFigureId == id, ct);
        if (fig is null)
            return NotFound();
        fig.IsPublished = decision == "publish";
        fig.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Redirect("/SADMIN/queue/drafts");
    }

    [HttpGet("/SADMIN/queue/onboarding")]
    public async Task<IActionResult> Onboarding(CancellationToken ct)
    {
        var rows = await _db.OnboardingSubmissions.AsNoTracking()
            .Where(s => s.Status == "PENDING_REVIEW")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
        return View(rows);
    }

    [HttpPost("/SADMIN/queue/onboarding/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideOnboarding(long id, string decision, string? reason, CancellationToken ct)
    {
        var row = await _db.OnboardingSubmissions.FirstOrDefaultAsync(s => s.OnboardingSubmissionId == id, ct);
        if (row is null)
            return NotFound();
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        row.ReviewedByUserId = userId;
        row.ReviewedAt = DateTime.UtcNow;
        row.DecisionReason = reason;
        row.UpdatedAt = DateTime.UtcNow;
        row.Status = decision == "approve" ? "APPROVED" : "REJECTED";
        await _db.SaveChangesAsync(ct);
        return Redirect("/SADMIN/queue/onboarding");
    }

    [HttpGet("/SADMIN/shops")]
    public async Task<IActionResult> Shops(
        string? q, string? state, string? claim, string? listing, string? designation,
        int? page, int? pageSize, long? inspect, CancellationToken ct)
    {
        var size = pageSize is 50 or 100 ? pageSize.Value : 25;
        var p = page is > 0 ? page.Value : 1;
        var query = _db.Shops.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.StateCode == state.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.City.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(claim))
            query = query.Where(s => s.ClaimStatus == claim.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(listing))
            query = query.Where(s => s.ListingStatus == listing.Trim().ToUpperInvariant());
        if (string.Equals(designation, "featured", StringComparison.OrdinalIgnoreCase))
            query = query.Where(s => s.IsEditorialFeatured);
        else if (string.Equals(designation, "destination", StringComparison.OrdinalIgnoreCase))
            query = query.Where(s => s.SourceSystem == "TOP100-2026-10-22"
                || (s.ImportNotes != null && s.ImportNotes.Contains("Destination shop")));
        else if (string.Equals(designation, "none", StringComparison.OrdinalIgnoreCase))
            query = query.Where(s => !s.IsEditorialFeatured
                && s.SourceSystem != "TOP100-2026-10-22"
                && (s.ImportNotes == null || !s.ImportNotes.Contains("Destination shop")));

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(s => s.StateCode).ThenBy(s => s.Name)
            .Skip((p - 1) * size).Take(size).ToListAsync(ct);
        var from = DateTime.UtcNow.Date.AddDays(-6);
        var counts = await _db.ShopImpressions.AsNoTracking()
            .Where(i => !i.IsBot && i.CreatedAt >= from && !i.Surface.StartsWith("CLICK_"))
            .GroupBy(i => i.ShopId)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);

        Shop? inspectShop = null;
        ShopMember? owner = null;
        if (inspect is long id)
        {
            inspectShop = rows.FirstOrDefault(s => s.ShopId == id)
                ?? await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == id, ct);
            if (inspectShop is not null)
                owner = await _db.ShopMembers.AsNoTracking().Include(m => m.User)
                    .FirstOrDefaultAsync(m => m.ShopId == id && m.RevokedAt == null && m.MemberRole == "OWNER", ct);
        }

        return View(new SadminShopListVm
        {
            Rows = rows,
            Inspect = inspectShop,
            Owner = owner,
            Q = q ?? "",
            State = state ?? "",
            Claim = claim ?? "",
            Listing = listing ?? "",
            Designation = designation ?? "",
            Page = p,
            PageSize = size,
            Total = total,
            Imp7 = counts,
            CanImpersonate = ImpersonationService.CanImpersonate(User)
        });
    }


    private static string? Str(JsonElement r, string name)
        => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [HttpGet("/SADMIN/shops/{id:long}/proxy")]
    public async Task<IActionResult> Proxy(long id, CancellationToken ct)
    {
        var shop = await _db.Shops.Include(s => s.Hours).FirstOrDefaultAsync(s => s.ShopId == id, ct);
        if (shop is null)
            return NotFound();
        return View(shop);
    }

    [HttpPost("/SADMIN/shops/{id:long}/proxy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Proxy(long id, ShopEditForm form, CancellationToken ct)
    {
        var shop = await _db.Shops.Include(s => s.Hours).FirstOrDefaultAsync(s => s.ShopId == id, ct);
        if (shop is null)
            return NotFound();
        var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _owner.PublishImmediateAsync(shop, userId, form, HoursFromForm(), ct);
        TempData["Saved"] = "Owner-proxy edit published.";
        return Redirect("/SADMIN/shops/" + id + "/proxy");
    }

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

    private static string Slugify(string text)
    {
        var chars = text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }
}

public sealed class SadminShopListVm
{
    public List<Shop> Rows { get; set; } = [];
    public Shop? Inspect { get; set; }
    public ShopMember? Owner { get; set; }
    public string Q { get; set; } = "";
    public string State { get; set; } = "";
    public string Claim { get; set; } = "";
    public string Listing { get; set; } = "";
    public string Designation { get; set; } = "";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int Total { get; set; }
    public Dictionary<long, int> Imp7 { get; set; } = [];
    public bool CanImpersonate { get; set; }
}
