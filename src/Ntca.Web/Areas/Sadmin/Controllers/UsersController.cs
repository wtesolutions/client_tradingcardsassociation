using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class UsersController : Controller
{
    private readonly NtcaDbContext _db;
    public UsersController(NtcaDbContext db) => _db = db;

    [HttpGet("/SADMIN/users")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = _db.Users.AsNoTracking().Include(u => u.Roles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.DisplayName.Contains(term) || u.Username.Contains(term));
        }
        var users = await query.OrderBy(u => u.DisplayName).Take(200).ToListAsync(ct);
        var ids = users.Select(u => u.UserId).ToList();
        var members = await _db.ShopMembers.AsNoTracking().Include(m => m.Shop)
            .Where(m => ids.Contains(m.UserId) && m.RevokedAt == null && m.MemberRole == "OWNER")
            .ToListAsync(ct);
        ViewBag.Q = q ?? "";
        ViewBag.Members = members.GroupBy(m => m.UserId).ToDictionary(g => g.Key, g => g.ToList());
        ViewBag.CanImpersonate = ImpersonationService.CanImpersonate(User);
        return View(users);
    }
}
