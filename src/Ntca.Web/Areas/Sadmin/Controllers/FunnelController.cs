using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

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
        var report = FunnelReport.Build(funnels, events);
        ViewBag.Started = report.Started;
        ViewBag.Registered = report.Registered;
        ViewBag.Confirmed = report.Confirmed;
        ViewBag.OnboardingStarted = report.OnboardingStarted;
        ViewBag.OnboardingDone = report.OnboardingCompleted;
        ViewBag.OnboardingSkipped = report.OnboardingSkipped;
        ViewBag.ByRole = report.ByRole;
        ViewBag.ByUtm = report.ByUtm;
        ViewBag.RegisterSteps = report.RegisterSteps;
        ViewBag.OnboardSteps = report.OnboardSteps;
        ViewBag.Recent = report.Recent;
        return View();
    }
}
