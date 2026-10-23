using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class GuideController : Controller
{
    private readonly NtcaDbContext _db;
    public GuideController(NtcaDbContext db) => _db = db;

    [HttpGet("/new-to-cards")]
    public IActionResult Alias() => RedirectPermanent("/new-to-trading-cards");

    [HttpGet("/new-to-trading-cards")]
    [HttpGet("/guide")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var terms = await _db.GlossaryTerms.AsNoTracking()
            .Where(t => t.IsPublished)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Term)
            .ToListAsync(ct);
        var grades = await _db.ConditionGrades.AsNoTracking()
            .Where(g => g.IsPublished)
            .OrderBy(g => g.Rank)
            .ToListAsync(ct);
        return View((terms, grades));
    }
}
