using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

namespace Ntca.Web.Areas.Sadmin.Controllers;

[Area("Sadmin")]
[Authorize(Policy = "Sadmin", AuthenticationSchemes = AuthSchemes.Sadmin)]
public sealed class AgentsController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly AgentUaRuleCache _cache;

    public AgentsController(NtcaDbContext db, AgentUaRuleCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet("/SADMIN/agents")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var from = DateTime.UtcNow.AddDays(-30);
        var visits = await _db.AgentVisits.AsNoTracking().Where(v => v.CreatedAt >= from).ToListAsync(ct);
        var inquiries = await _db.AgentInquiries.AsNoTracking().Where(i => i.CreatedAt >= from).OrderByDescending(i => i.CreatedAt).Take(40).ToListAsync(ct);
        ViewBag.ByAssistant = visits.GroupBy(v => v.AssistantName).Select(g => new { Name = g.Key, N = g.Count() }).OrderByDescending(x => x.N).ToList();
        ViewBag.ByRole = inquiries.Where(i => i.HumanRole != null).GroupBy(i => i.HumanRole!).Select(g => new { Name = g.Key, N = g.Count() }).OrderByDescending(x => x.N).ToList();
        ViewBag.ByState = inquiries.Where(i => i.StateCode != null).GroupBy(i => i.StateCode!).Select(g => new { Name = g.Key, N = g.Count() }).OrderByDescending(x => x.N).ToList();
        ViewBag.ByInterest = inquiries
            .Where(i => !string.IsNullOrEmpty(i.Interests))
            .SelectMany(i => i.Interests!.Split(',', StringSplitOptions.TrimEntries))
            .GroupBy(x => x)
            .Select(g => new { Name = g.Key, N = g.Count() })
            .OrderByDescending(x => x.N)
            .ToList();
        ViewBag.Questions = inquiries.Where(i => !string.IsNullOrEmpty(i.QuestionExcerpt)).Take(15).ToList();
        ViewBag.Visits7 = visits.Count(v => v.CreatedAt >= DateTime.UtcNow.AddDays(-7));
        ViewBag.Inquiries7 = inquiries.Count(i => i.CreatedAt >= DateTime.UtcNow.AddDays(-7));
        ViewBag.Rules = await _db.AgentUaRules.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(ct);
        return View(inquiries);
    }

    [HttpPost("/SADMIN/agents/rules")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRule(string token, string assistant, string role, short sort, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(assistant))
            return Redirect("/SADMIN/agents");
        role = role is "USER_ASSISTANT" or "SEARCH" or "TRAINING" ? role : "SEARCH";
        _db.AgentUaRules.Add(new AgentUaRule
        {
            Token = token.Trim(),
            AssistantName = assistant.Trim(),
            Role = role,
            IsEnabled = true,
            SortOrder = sort,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        _cache.Invalidate();
        return Redirect("/SADMIN/agents");
    }

    [HttpPost("/SADMIN/agents/rules/{id:long}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(long id, CancellationToken ct)
    {
        var row = await _db.AgentUaRules.FirstOrDefaultAsync(r => r.AgentUaRuleId == id, ct);
        if (row is not null)
        {
            row.IsEnabled = !row.IsEnabled;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _cache.Invalidate();
        }
        return Redirect("/SADMIN/agents");
    }
}
