using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;

namespace Ntca.Web.Services;

public sealed class ImpersonationState
{
    public bool Active { get; init; }
    public long? StaffUserId { get; init; }
    public string? StaffName { get; init; }
    public long? OwnerUserId { get; init; }
    public string? OwnerName { get; init; }
}

public sealed class ImpersonationService
{
    public const string StaffIdClaim = "impersonator_id";
    public const string StaffNameClaim = "impersonator_name";
    public const string ExpiresClaim = "impersonator_exp";
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(60);

    private readonly NtcaDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<ImpersonationService> _log;

    public ImpersonationService(NtcaDbContext db, IHttpContextAccessor http, ILogger<ImpersonationService> log)
    {
        _db = db;
        _http = http;
        _log = log;
    }

    public static bool CanImpersonate(ClaimsPrincipal staff)
        => staff.IsInRole("SUPERADMIN") || staff.IsInRole("ADMIN");

    public ImpersonationState Read(HttpContext? http = null)
    {
        var ctx = http ?? _http.HttpContext;
        if (ctx?.Items["Impersonation"] is ImpersonationState cached)
            return cached;
        var user = ctx?.User;
        if (user?.Identity?.IsAuthenticated != true || !user.HasClaim(c => c.Type == StaffIdClaim))
            return new ImpersonationState();
        return new ImpersonationState
        {
            Active = true,
            StaffUserId = long.TryParse(user.FindFirstValue(StaffIdClaim), out var sid) ? sid : null,
            StaffName = user.FindFirstValue(StaffNameClaim),
            OwnerUserId = long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : null,
            OwnerName = user.FindFirstValue(ClaimTypes.Name)
        };
    }

    public async Task<string?> StartAsync(ClaimsPrincipal staff, long shopId, string? createEmail, CancellationToken ct)
    {
        if (!CanImpersonate(staff))
            return "Only SUPERADMIN or ADMIN can impersonate an owner.";
        if (!long.TryParse(staff.FindFirstValue(ClaimTypes.NameIdentifier), out var staffId))
            return "Staff session is missing.";
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.ShopId == shopId, ct);
        if (shop is null)
            return "Shop not found.";

        var member = await _db.ShopMembers.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.ShopId == shopId && m.RevokedAt == null && m.MemberRole == "OWNER", ct);
        AppUser? owner = member?.User;
        if (owner is null)
        {
            var email = (createEmail ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return "This shop has no owner account. Enter an email to create one, or use owner-proxy edit.";
            owner = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (owner is null)
            {
                owner = new AppUser
                {
                    PublicId = Guid.NewGuid(),
                    Email = email,
                    Username = email.Length <= 40 ? email : email[..40],
                    DisplayName = shop.Name + " owner",
                    AccountStatus = "ACTIVE",
                    IsStaff = false,
                    EmailVerifiedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    MembershipLevel = "SUPPORTER"
                };
                _db.Users.Add(owner);
                await _db.SaveChangesAsync(ct);
            }
            if (owner.IsStaff)
                return "That email belongs to a staff account and cannot be impersonated.";
            _db.ShopMembers.Add(new ShopMember
            {
                ShopId = shopId,
                UserId = owner.UserId,
                MemberRole = "OWNER",
                GrantedByUserId = staffId,
                GrantedAt = DateTime.UtcNow
            });
            shop.ClaimStatus = "CLAIMED";
            shop.ClaimedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        else if (owner.IsStaff || await IsStaffUserAsync(owner.UserId, ct))
            return "Cannot impersonate a staff or SADMIN account.";

        var ctx = _http.HttpContext ?? throw new InvalidOperationException("No HTTP context.");
        var exp = DateTimeOffset.UtcNow.Add(Ttl);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner.UserId.ToString()),
            new(ClaimTypes.Email, owner.Email),
            new(ClaimTypes.Name, owner.DisplayName),
            new("username", owner.Username),
            new(StaffIdClaim, staffId.ToString()),
            new(StaffNameClaim, staff.FindFirstValue(ClaimTypes.Name) ?? staff.Identity?.Name ?? "staff"),
            new(ExpiresClaim, exp.ToUnixTimeSeconds().ToString())
        };
        await ctx.SignInAsync(AuthSchemes.Owner, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Owner)), new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = exp
        });
        await AuditAsync("IMPERSONATE_START", "Shop", shopId, staffId, owner.UserId,
            $"{{\"shopId\":{shopId},\"ownerEmail\":\"{owner.Email.Replace("\"", "")}\"}}", ct);
        _log.LogWarning("Staff {Staff} impersonating owner {Owner} for shop {Shop}", staffId, owner.UserId, shopId);
        return null;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        var ctx = _http.HttpContext;
        if (ctx is null)
            return;
        var state = Read(ctx);
        await ctx.SignOutAsync(AuthSchemes.Owner);
        if (state.Active)
            await AuditAsync("IMPERSONATE_STOP", "AppUser", state.OwnerUserId ?? 0, state.StaffUserId, state.OwnerUserId, "{\"stop\":true}", ct);
    }

    public async Task AuditAsync(string action, string entityType, long entityId, long? staffId, long? ownerId, string json, CancellationToken ct)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EntityType = entityType.Length > 40 ? entityType[..40] : entityType,
                EntityId = entityId,
                Action = action.Length > 30 ? action[..30] : action,
                UserId = ownerId ?? staffId,
                ImpersonatorUserId = staffId,
                ChangesJson = json,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Impersonation audit failed for {Action}", action);
        }
    }

    public string Attribution(ImpersonationState state, string fallbackName)
    {
        if (state.Active)
            return $"by staff {state.StaffName} as owner {state.OwnerName}";
        return fallbackName;
    }

    private async Task<bool> IsStaffUserAsync(long userId, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Roles).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
            return false;
        if (user.IsStaff)
            return true;
        return user.Roles.Any(r => r.RoleCode is "SUPERADMIN" or "ADMIN" or "MODERATOR" or "EDITOR");
    }
}

public sealed class ImpersonationMiddleware
{
    private readonly RequestDelegate _next;
    public ImpersonationMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, ImpersonationService impersonation)
    {
        var owner = await ctx.AuthenticateAsync(AuthSchemes.Owner);
        if (owner.Succeeded && owner.Principal.HasClaim(c => c.Type == ImpersonationService.StaffIdClaim))
        {
            var expRaw = owner.Principal.FindFirstValue(ImpersonationService.ExpiresClaim);
            if (long.TryParse(expRaw, out var exp) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
            {
                await impersonation.StopAsync(ctx.RequestAborted);
            }
            else
            {
                ctx.Items["Impersonation"] = new ImpersonationState
                {
                    Active = true,
                    StaffUserId = long.TryParse(owner.Principal.FindFirstValue(ImpersonationService.StaffIdClaim), out var sid) ? sid : null,
                    StaffName = owner.Principal.FindFirstValue(ImpersonationService.StaffNameClaim),
                    OwnerUserId = long.TryParse(owner.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var oid) ? oid : null,
                    OwnerName = owner.Principal.FindFirstValue(ClaimTypes.Name)
                };
            }
        }
        await _next(ctx);
    }
}
