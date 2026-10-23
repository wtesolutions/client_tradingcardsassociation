namespace Ntca.Web.Services;

public sealed class AgentConciergeMiddleware
{
    public const string ItemKey = "Ntca.AiVisitor";
    private readonly RequestDelegate _next;

    public AgentConciergeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, AgentUaRuleCache rules, AgentVisitBuffer visits)
    {
        var visitor = AgentClassifier.Classify(ctx.Request.Headers.UserAgent.ToString(), rules.Enabled(), ctx.Request);
        ctx.Items[ItemKey] = visitor;
        if (visitor.IsAgent)
        {
            ctx.Response.Headers.Append("Link", "</.well-known/ai-concierge>; rel=\"agent-concierge\"");
            ctx.Response.Headers["X-NTCA-Agent-Concierge"] = "/agent";
            ctx.Response.Headers["X-NTCA-Agent-Role"] = visitor.Role;
            if (visitor.IsVerified)
                ctx.Response.Headers["X-NTCA-Agent-Verified"] = "1";
        }
        await _next(ctx);
        if (visitor.IsAgent && ShouldRecord(ctx))
        {
            var path = ctx.Request.Path.Value ?? "/";
            if (path.Length > 200)
                path = path[..200];
            visits.Enqueue(new AgentVisitHit(visitor.AssistantName, visitor.Role, visitor.IsVerified, path, DateTime.UtcNow));
        }
    }

    private static bool ShouldRecord(HttpContext ctx)
    {
        if (ctx.Response.StatusCode is < 200 or >= 400)
            return false;
        var path = ctx.Request.Path.Value ?? "";
        if (path.StartsWith("/SADMIN", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/owner", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    public static AiVisitor? Current(HttpContext? ctx)
        => ctx?.Items[ItemKey] as AiVisitor;
}
