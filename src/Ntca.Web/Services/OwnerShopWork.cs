using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Areas.Owner.Controllers;

namespace Ntca.Web.Services;

public sealed class OwnerShopWork
{
    private readonly NtcaDbContext _db;
    private readonly ImpersonationService _imp;

    public OwnerShopWork(NtcaDbContext db, ImpersonationService imp)
    {
        _db = db;
        _imp = imp;
    }

    public async Task<List<Shop>> OwnedShopsAsync(long userId, CancellationToken ct)
        => await _db.ShopMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.RevokedAt == null)
            .Include(m => m.Shop)
            .Select(m => m.Shop!)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<Shop?> OwnedAsync(long userId, long? shopId, CancellationToken ct)
    {
        var q = _db.ShopMembers.Include(m => m.Shop).ThenInclude(s => s!.Hours)
            .Where(m => m.UserId == userId && m.RevokedAt == null);
        if (shopId is long id)
            q = q.Where(m => m.ShopId == id);
        return (await q.OrderBy(m => m.ShopMemberId).FirstOrDefaultAsync(ct))?.Shop;
    }

    public async Task SubmitIdentityAsync(Shop shop, long userId, ShopEditForm form, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            kind = "identity",
            form.Name,
            form.AddressLine1,
            form.City,
            form.StateCode,
            form.PostalCode
        });
        _db.ShopEditRequests.Add(new ShopEditRequest
        {
            PublicId = Guid.NewGuid(),
            ShopId = shop.ShopId,
            RequestedByUserId = userId,
            ProposedJson = payload,
            RequestKind = "REVIEW",
            Status = "PENDING_REVIEW",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await Audit(shop.ShopId, userId, "OWNER_EDIT_Q", payload, ct);
    }

    public async Task PublishImmediateAsync(Shop shop, long userId, ShopEditForm form, IReadOnlyList<HourRow> hours, CancellationToken ct)
    {
        shop.Phone = Blank(form.Phone);
        shop.WebsiteUrl = Blank(form.WebsiteUrl);
        shop.PublicEmail = Blank(form.PublicEmail);
        shop.Description = Blank(form.Description);
        shop.PrimarySpecialty = Blank(form.PrimarySpecialty);
        shop.UpdatedAt = DateTime.UtcNow;
        ShopAdminCopy.AddLock(shop, "description");
        ShopAdminCopy.AddLock(shop, "hours");
        await ReplaceHoursAsync(shop, hours, ct);
        _db.ShopEditRequests.Add(new ShopEditRequest
        {
            PublicId = Guid.NewGuid(),
            ShopId = shop.ShopId,
            RequestedByUserId = userId,
            ProposedJson = JsonSerializer.Serialize(new { kind = "immediate", form.Phone, form.WebsiteUrl, form.Description }),
            RequestKind = "FYI",
            Status = "FYI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await Audit(shop.ShopId, userId, "OWNER_LIVE", "{\"kind\":\"immediate\"}", ct);
    }

    public async Task<string> SubmitClaimAsync(long userId, string email, long shopId, CancellationToken ct)
    {
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.ShopId == shopId, ct);
        if (shop is null)
            return "Shop not found.";
        if (await _db.ShopMembers.AnyAsync(m => m.ShopId == shopId && m.RevokedAt == null && m.MemberRole == "OWNER", ct))
            return "That shop already has an owner.";
        if (await _db.ShopClaimRequests.AnyAsync(c => c.ShopId == shopId && c.RequestedByUserId == userId && (c.Status == "PENDING" || c.Status == "VERIFYING"), ct))
            return "You already have a claim waiting for staff.";

        var auto = ShopAdminCopy.EmailDomainMatchesWebsite(email, shop.WebsiteUrl);
        var row = new ShopClaimRequest
        {
            ShopId = shopId,
            RequestedByUserId = userId,
            ClaimType = "OWNER",
            Status = auto ? "APPROVED" : "PENDING",
            VerificationMethod = auto ? "EMAIL_DOMAIN" : "ADMIN",
            ContactEmail = email,
            SubmittedAt = DateTime.UtcNow,
            VerifiedAt = auto ? DateTime.UtcNow : null
        };
        _db.ShopClaimRequests.Add(row);
        if (auto)
        {
            _db.ShopMembers.Add(new ShopMember
            {
                ShopId = shopId,
                UserId = userId,
                MemberRole = "OWNER",
                GrantedAt = DateTime.UtcNow
            });
            shop.ClaimStatus = "CLAIMED";
            shop.ClaimedAt = DateTime.UtcNow;
            shop.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            shop.ClaimStatus = "PENDING";
            shop.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        await Audit(shopId, userId, auto ? "CLAIM_AUTO" : "CLAIM_SUBMIT", $"{{\"auto\":{auto.ToString().ToLowerInvariant()}}}", ct);
        return auto
            ? "Claim approved — your email domain matches the shop website."
            : "Claim submitted. Staff will review it in the SADMIN claim queue.";
    }

    public async Task DecideClaimAsync(long claimId, long staffId, bool approve, string? reason, CancellationToken ct)
    {
        var row = await _db.ShopClaimRequests.Include(c => c.Shop).FirstOrDefaultAsync(c => c.ClaimRequestId == claimId, ct);
        if (row is null)
            return;
        row.ReviewedByUserId = staffId;
        row.ReviewedAt = DateTime.UtcNow;
        row.DecisionReason = reason;
        row.Status = approve ? "APPROVED" : "REJECTED";
        if (approve && row.Shop is not null)
        {
            if (!await _db.ShopMembers.AnyAsync(m => m.ShopId == row.ShopId && m.UserId == row.RequestedByUserId && m.RevokedAt == null, ct))
            {
                _db.ShopMembers.Add(new ShopMember
                {
                    ShopId = row.ShopId,
                    UserId = row.RequestedByUserId,
                    MemberRole = "OWNER",
                    ClaimRequestId = row.ClaimRequestId,
                    GrantedByUserId = staffId,
                    GrantedAt = DateTime.UtcNow
                });
            }
            row.Shop.ClaimStatus = "CLAIMED";
            row.Shop.ClaimedAt ??= DateTime.UtcNow;
            row.VerifiedAt = DateTime.UtcNow;
            row.VerificationMethod = "ADMIN";
        }
        else if (row.Shop is not null && row.Shop.ClaimStatus == "PENDING")
            row.Shop.ClaimStatus = "UNCLAIMED";
        await _db.SaveChangesAsync(ct);
        await Audit(row.ShopId, row.RequestedByUserId, approve ? "CLAIM_OK" : "CLAIM_NO", $"{{\"staffId\":{staffId}}}", ct);
    }

    public async Task ApplyApprovedIdentityAsync(Shop shop, JsonElement r)
    {
        shop.Name = Str(r, "Name") ?? shop.Name;
        shop.AddressLine1 = Str(r, "AddressLine1") ?? shop.AddressLine1;
        shop.City = Str(r, "City") ?? shop.City;
        shop.StateCode = (Str(r, "StateCode") ?? shop.StateCode).ToUpperInvariant();
        shop.PostalCode = Str(r, "PostalCode") ?? shop.PostalCode;
        shop.UpdatedAt = DateTime.UtcNow;
        ShopAdminCopy.AddLock(shop, "location");
        await Task.CompletedTask;
    }

    private async Task ReplaceHoursAsync(Shop shop, IReadOnlyList<HourRow> hours, CancellationToken ct)
    {
        var existing = await _db.ShopHours.Where(h => h.ShopId == shop.ShopId).ToListAsync(ct);
        _db.ShopHours.RemoveRange(existing);
        foreach (var h in hours)
        {
            _db.ShopHours.Add(new ShopHours
            {
                ShopId = shop.ShopId,
                DayOfWeek = h.Day,
                IsClosed = h.Closed,
                OpensAt = h.Closed ? null : ParseTime(h.Opens),
                ClosesAt = h.Closed ? null : ParseTime(h.Closes)
            });
        }
    }

    private async Task Audit(long shopId, long userId, string action, string json, CancellationToken ct)
    {
        var state = _imp.Read();
        var extra = state.Active
            ? $"{{\"attribution\":\"{_imp.Attribution(state, "owner")}\",\"inner\":{json}}}"
            : json;
        await _imp.AuditAsync(action, "Shop", shopId, state.StaffUserId, userId, extra, ct);
    }

    private static string? Str(JsonElement r, string name)
        => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static TimeOnly? ParseTime(string? raw)
        => TimeOnly.TryParse(raw, out var t) ? t : null;
}

public sealed class HourRow
{
    public byte Day { get; set; }
    public bool Closed { get; set; }
    public string? Opens { get; set; }
    public string? Closes { get; set; }
}
