using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class AgentController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly AgentAnswerBuilder _answers;

    public AgentController(NtcaDbContext db, AgentAnswerBuilder answers)
    {
        _db = db;
        _answers = answers;
    }

    [HttpGet("/.well-known/ai-concierge")]
    public IActionResult WellKnown()
    {
        var origin = Origin();
        return Json(new
        {
            name = "NTCA AI Concierge",
            welcome = AgentCopy.Welcome,
            privacy = AgentCopy.Privacy,
            human_roles = AgentCopy.HumanRoles,
            ask = origin + "/api/v1/agent/ask",
            page = origin + "/agent",
            llms = origin + "/llms.txt",
            note = "Same page content for every visitor. Extra welcome + this API for assistants. No LLM required."
        });
    }

    [HttpGet("/agent")]
    public async Task<IActionResult> Page(string? role, string? zip, string? state, string? city, string? interests, string? q, string? budget, string? dates, CancellationToken ct)
    {
        var ask = new AgentAsk
        {
            Assistant = AgentConciergeMiddleware.Current(HttpContext)?.AssistantName,
            HumanRole = role,
            Location = new AgentLocation { City = city, State = state, Zip = zip },
            Interests = string.IsNullOrWhiteSpace(interests) ? null : interests.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            Budget = budget,
            Dates = dates,
            Question = q
        };
        var sanitized = AgentPrivacy.Sanitize(default, ask);
        var answer = await _answers.BuildAsync(sanitized, Origin(), ct);
        if (HasIntent(sanitized))
            await StoreInquiry(sanitized, ct);
        ViewBag.Answer = answer;
        ViewBag.Ask = sanitized;
        return View();
    }

    [HttpPost("/api/v1/agent/ask")]
    [EnableRateLimiting("agent-ask")]
    public async Task<IActionResult> Ask([FromBody] JsonElement body, CancellationToken ct)
    {
        AgentAsk ask;
        try
        {
            ask = JsonSerializer.Deserialize<AgentAsk>(body.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AgentAsk();
        }
        catch
        {
            return BadRequest(new { error = "JSON body required." });
        }
        var visitor = AgentConciergeMiddleware.Current(HttpContext);
        if (string.IsNullOrWhiteSpace(ask.Assistant) && visitor?.IsAgent == true)
            ask.Assistant = visitor.AssistantName;
        var sanitized = AgentPrivacy.Sanitize(body, ask);
        await StoreInquiry(sanitized, ct);
        var answer = await _answers.BuildAsync(sanitized, Origin(), ct);
        return Json(answer);
    }

    private async Task StoreInquiry(AgentAskResult ask, CancellationToken ct)
    {
        var visitor = AgentConciergeMiddleware.Current(HttpContext);
        _db.AgentInquiries.Add(new AgentInquiry
        {
            AssistantName = ask.Assistant ?? visitor?.AssistantName,
            HumanRole = ask.HumanRole,
            StateCode = ask.StateCode,
            Zip3 = ask.Zip3,
            Interests = ask.Interests.Count == 0 ? null : string.Join(",", ask.Interests),
            QuestionExcerpt = string.IsNullOrWhiteSpace(ask.Question) ? null : ask.Question.Length <= 240 ? ask.Question : ask.Question[..240],
            IsVerified = visitor?.IsVerified == true,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    private static bool HasIntent(AgentAskResult ask)
        => ask.HumanRole != null || ask.StateCode != null || ask.Interests.Count > 0 || !string.IsNullOrWhiteSpace(ask.Question);

    private string Origin() => $"{Request.Scheme}://{Request.Host}";
}
