using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class QueueController : Controller
{
    private readonly NtcaDbContext _db;
    public QueueController(NtcaDbContext db) => _db = db;

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
        var rows = await _db.ShopEditRequests.AsNoTracking()
            .Include(e => e.Shop)
            .Where(s => s.Status == "PENDING_REVIEW")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
        return View(rows);
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
        if (decision == "approve" && row.Shop is not null)
        {
            row.Status = "APPROVED";
            using var doc = JsonDocument.Parse(row.ProposedJson);
            var r = doc.RootElement;
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
        else
        {
            row.Status = "REJECTED";
        }
        await _db.SaveChangesAsync(ct);
        return Redirect("/SADMIN/queue/edits");
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

    [HttpGet("/SADMIN/shops")]
    public async Task<IActionResult> Shops(string? q, string? state, CancellationToken ct)
    {
        var query = _db.Shops.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(s => s.StateCode == state.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.City.Contains(term));
        }
        var rows = await query.OrderBy(s => s.StateCode).ThenBy(s => s.Name).Take(400).ToListAsync(ct);
        ViewBag.Q = q ?? "";
        ViewBag.State = state ?? "";
        return View(rows);
    }

    private static string? Str(JsonElement r, string name)
        => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Slugify(string text)
    {
        var chars = text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }
}
