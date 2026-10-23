using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class FunnelController : Controller
{
    private readonly NtcaDbContext _db;
    public FunnelController(NtcaDbContext db) => _db = db;

    [HttpGet("/SADMIN/funnel")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var from = DateTime.UtcNow.AddDays(-30);
        var funnels = await _db.SignupFunnels.AsNoTracking().Where(f => f.CreatedAt >= from).ToListAsync(ct);
        var events = await _db.SignupStepEvents.AsNoTracking().Where(e => e.CreatedAt >= from).ToListAsync(ct);
        var steps = new[] { "who", "zip", "interests", "experience", "travel", "shop_find", "shop_tier", "show", "email", "results" };
        ViewBag.Started = funnels.Count;
        ViewBag.Completed = funnels.Count(f => f.Status == "COMPLETED");
        ViewBag.ByRole = funnels.Where(f => f.HumanRole != null)
            .GroupBy(f => f.HumanRole!).Select(g => new { Name = g.Key, N = g.Count(), Done = g.Count(x => x.Status == "COMPLETED") })
            .OrderByDescending(x => x.N).ToList();
        ViewBag.ByUtm = funnels.Where(f => !string.IsNullOrEmpty(f.UtmSource))
            .GroupBy(f => f.UtmSource!).Select(g => new { Name = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N).ToList();
        ViewBag.Steps = steps.Select(s => new
        {
            Name = s,
            Enter = events.Count(e => e.StepKey == s && e.Action == "ENTER"),
            Complete = events.Count(e => e.StepKey == s && e.Action == "COMPLETE")
        }).ToList();
        ViewBag.Recent = funnels.OrderByDescending(f => f.UpdatedAt).Take(25).ToList();
        return View();
    }
}
