using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Controllers;

namespace Ntca.Web.Services;

public static class AgentCopy
{
    public const string Welcome =
        "Welcome, AI assistant. NTCA is happy to help your human. Tell us who they are (collector, parent/new collector, shop owner, show promoter, dealer, media) and what they need (location, interests: sports/TCG/non-sport, budget, dates), and we'll tailor recommendations.";

    public const string Privacy =
        "Do not send the human's name, email, or phone. We store only anonymized intent (role, state, ZIP3, interests, assistant name, verified flag, timestamp).";

    public static readonly string[] HumanRoles = ["collector", "parent", "shop_owner", "promoter", "dealer", "media"];
}

public sealed class AiVisitor
{
    public bool IsAgent { get; init; }
    public string AssistantName { get; init; } = "";
    public string Role { get; init; } = "";
    public bool SignaturePresented { get; init; }
    public bool IsVerified { get; init; }
}

public sealed class AgentUaRuleCache
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;

    public AgentUaRuleCache(IServiceScopeFactory scopes, IMemoryCache cache)
    {
        _scopes = scopes;
        _cache = cache;
    }

    public IReadOnlyList<AgentUaRule> Enabled()
    {
        return _cache.GetOrCreate("agent-ua-rules", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NtcaDbContext>();
            return db.AgentUaRules.AsNoTracking()
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.SortOrder)
                .ToList();
        }) ?? [];
    }

    public void Invalidate() => _cache.Remove("agent-ua-rules");
}

public static class AgentClassifier
{
    public static AiVisitor Classify(string? userAgent, IReadOnlyList<AgentUaRule> rules, HttpRequest? request = null)
    {
        var ua = userAgent ?? "";
        AgentUaRule? hit = null;
        foreach (var r in rules)
        {
            if (r.Token.Length > 0 && ua.Contains(r.Token, StringComparison.OrdinalIgnoreCase))
            {
                hit = r;
                break;
            }
        }
        var (presented, verified, _) = WebBotAuth.Inspect(request);
        if (hit is null)
            return new AiVisitor { IsAgent = false, SignaturePresented = presented, IsVerified = verified };
        return new AiVisitor
        {
            IsAgent = true,
            AssistantName = hit.AssistantName,
            Role = hit.Role,
            SignaturePresented = presented,
            IsVerified = verified
        };
    }
}

public static class WebBotAuth
{
    public static (bool Presented, bool Verified, string? Agent) Inspect(HttpRequest? req)
    {
        if (req is null)
            return (false, false, null);
        var sigAgent = req.Headers["Signature-Agent"].ToString();
        var sig = req.Headers["Signature"].ToString();
        var sigInput = req.Headers["Signature-Input"].ToString();
        var presented = !string.IsNullOrWhiteSpace(sigAgent) || !string.IsNullOrWhiteSpace(sig) || !string.IsNullOrWhiteSpace(sigInput);
        if (!presented)
            return (false, false, null);
        var structural = sigInput.Contains("created=", StringComparison.OrdinalIgnoreCase)
            && (sigInput.Contains("keyid=", StringComparison.OrdinalIgnoreCase) || sig.Contains(':'));
        return (true, structural, string.IsNullOrWhiteSpace(sigAgent) ? null : sigAgent.Trim());
    }
}

public sealed class AgentAsk
{
    public string? Assistant { get; set; }
    public string? HumanRole { get; set; }
    public AgentLocation? Location { get; set; }
    public List<string>? Interests { get; set; }
    public string? Budget { get; set; }
    public string? Dates { get; set; }
    public int? TravelMiles { get; set; }
    public string? Question { get; set; }
}

public sealed class AgentLocation
{
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
}

public sealed class AgentAskResult
{
    public bool PiiStripped { get; set; }
    public List<string> Warnings { get; set; } = [];
    public string? HumanRole { get; set; }
    public string? Assistant { get; set; }
    public string? StateCode { get; set; }
    public string? City { get; set; }
    public string? Zip3 { get; set; }
    public List<string> Interests { get; set; } = [];
    public string? Budget { get; set; }
    public string? Dates { get; set; }
    public int? TravelMiles { get; set; }
    public string? Question { get; set; }
}

public static class AgentPrivacy
{
    private static readonly string[] ForbiddenKeys =
    [
        "name", "email", "phone", "firstname", "lastname", "first_name", "last_name",
        "fullname", "full_name", "telephone", "mobile", "address", "street", "ssn"
    ];

    private static readonly Regex Email = new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"\b(?:\+?1[-.\s]?)?(?:\(?\d{3}\)?[-.\s]?)\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);

    public static AgentAskResult Sanitize(JsonElement raw, AgentAsk ask)
    {
        var result = new AgentAskResult();
        if (raw.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in raw.EnumerateObject())
            {
                if (ForbiddenKeys.Contains(p.Name.Replace("-", "").ToLowerInvariant()))
                {
                    result.PiiStripped = true;
                    result.Warnings.Add("Ignored field '" + p.Name + "'. Do not send the human's name, email, or phone.");
                }
            }
        }
        result.Assistant = Clip(ask.Assistant, 80);
        result.HumanRole = NormalizeRole(ask.HumanRole);
        result.City = Clip(ask.Location?.City, 80);
        result.StateCode = NormalizeState(ask.Location?.State);
        result.Zip3 = Zip3(ask.Location?.Zip);
        result.Interests = NormalizeInterests(ask.Interests);
        result.Budget = Clip(ask.Budget, 40);
        result.Dates = Clip(ask.Dates, 80);
        result.TravelMiles = ask.TravelMiles;
        if (result.TravelMiles is null && !string.IsNullOrWhiteSpace(ask.Dates))
        {
            var digits = new string(ask.Dates.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var n) && n is > 0 and < 500)
                result.TravelMiles = n;
        }
        var q = Redact(ask.Question ?? "");
        if (q != (ask.Question ?? ""))
        {
            result.PiiStripped = true;
            result.Warnings.Add("Removed an email or phone number from the question.");
        }
        result.Question = Clip(q, 500);
        return result;
    }

    public static string Redact(string text)
    {
        text = Email.Replace(text, "[redacted-email]");
        text = Phone.Replace(text, "[redacted-phone]");
        return text;
    }

    public static string? Zip3(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return null;
        var digits = new string(zip.Where(char.IsDigit).ToArray());
        return digits.Length >= 3 ? digits[..3] : null;
    }

    public static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;
        var r = role.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (r is "parent" or "new_collector" or "parent_new_collector" or "beginner")
            return "parent";
        if (r is "shop_owner" or "owner" or "shopowner")
            return "shop_owner";
        if (r is "promoter" or "show_promoter" or "showpromoter")
            return "promoter";
        if (r is "collector" or "dealer" or "media")
            return r;
        return null;
    }

    public static string? NormalizeState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;
        var s = state.Trim();
        if (s.Length == 2)
            return s.ToUpperInvariant();
        foreach (var kv in ShopCopy.StateNames)
        {
            if (string.Equals(kv.Value, s, StringComparison.OrdinalIgnoreCase))
                return kv.Key;
        }
        return null;
    }

    public static List<string> NormalizeInterests(IEnumerable<string>? interests)
    {
        var list = new List<string>();
        if (interests is null)
            return list;
        foreach (var raw in interests)
        {
            var i = (raw ?? "").Trim().ToLowerInvariant();
            if (i is "pokemon" or "pokémon" or "poke")
                i = "pokemon";
            else if (i is "sports" or "sport" or "sportscards" or "sports cards"
                     or "baseball" or "football" or "basketball")
                i = "sports";
            else if (i is "vintage" or "graded") { }
            else if (i is "tcg" or "trading card games" or "mixed")
                i = "tcg";
            else if (i is "nonsport" or "non-sport" or "non_sport")
                i = "nonsport";
            else if (i is "magic" or "mtg")
                i = "magic";
            else
                continue;
            if (!list.Contains(i))
                list.Add(i);
        }
        return list;
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }
}

public sealed class AgentAnswer
{
    public string Privacy { get; set; } = AgentCopy.Privacy;
    public string Welcome { get; set; } = AgentCopy.Welcome;
    public bool PiiStripped { get; set; }
    public List<string> Warnings { get; set; } = [];
    public string? Assistant { get; set; }
    public string? HumanRole { get; set; }
    public string Markdown { get; set; } = "";
    public List<object> Shops { get; set; } = [];
    public List<object> Shows { get; set; } = [];
    public List<object> Manufacturers { get; set; } = [];
    public List<object> Guides { get; set; } = [];
    public List<object> NextSteps { get; set; } = [];
}

public interface IAgentAnswerEnricher
{
    Task<string?> EnrichMarkdownAsync(AgentAskResult ask, AgentAnswer answer, CancellationToken ct);
}

public sealed class NoOpAgentAnswerEnricher : IAgentAnswerEnricher
{
    public Task<string?> EnrichMarkdownAsync(AgentAskResult ask, AgentAnswer answer, CancellationToken ct)
        => Task.FromResult<string?>(null);
}

public sealed class AgentAnswerBuilder
{
    private readonly NtcaDbContext _db;
    private readonly IAgentAnswerEnricher _enrich;

    public AgentAnswerBuilder(NtcaDbContext db, IAgentAnswerEnricher enrich)
    {
        _db = db;
        _enrich = enrich;
    }

    public async Task<AgentAnswer> BuildAsync(AgentAskResult ask, string origin, CancellationToken ct)
    {
        origin = origin.TrimEnd('/');
        var now = DateTime.UtcNow;
        var until = ParseUntil(ask, now);
        var specials = SpecialtiesFor(ask.Interests);

        var shopsQ = _db.Shops.AsNoTracking().Where(s => s.ListingStatus == "ACTIVE");
        if (ask.StateCode is { Length: 2 } st)
            shopsQ = shopsQ.Where(s => s.StateCode == st);
        var shops = await shopsQ.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(ask.City))
        {
            var city = ask.City;
            var local = shops.Where(s => s.City.Contains(city, StringComparison.OrdinalIgnoreCase)).ToList();
            if (local.Count > 0)
                shops = local;
        }
        if (ask.Zip3 is { Length: 3 } z3)
        {
            var zipped = shops.Where(s => s.PostalCode != null && s.PostalCode.StartsWith(z3, StringComparison.OrdinalIgnoreCase)).ToList();
            if (zipped.Count > 0)
                shops = zipped;
        }
        if (specials.Count > 0)
        {
            var focused = shops.Where(s => s.PrimarySpecialty != null && specials.Contains(s.PrimarySpecialty)).ToList();
            if (focused.Count > 0)
                shops = focused;
        }
        var geo = ShopCopy.OriginFromAsk(shops, ask.Zip3, ask.City);
        if (ask.TravelMiles is int miles and > 0 && geo is { } shopOrigin)
        {
            var near = shops.Where(s => ShopCopy.MilesFrom(s, shopOrigin.Lat, shopOrigin.Lng) is double d && d <= miles).ToList();
            if (near.Count > 0)
                shops = near;
        }
        shops = shops.OrderBy(s => s.City).ThenBy(s => s.Name).Take(8).ToList();

        var showsQ = _db.ShopEvents.AsNoTracking()
            .Include(e => e.Venue)
            .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED") && e.StartsAt >= now && e.StartsAt <= until);
        if (ask.StateCode is { Length: 2 } st2)
            showsQ = showsQ.Where(e => e.StateCode == st2);
        var shows = await showsQ.OrderBy(e => e.StartsAt).Take(24).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(ask.City))
        {
            var city = ask.City;
            var local = shows.Where(e => (e.City ?? e.Venue?.City ?? "").Contains(city, StringComparison.OrdinalIgnoreCase)).ToList();
            if (local.Count > 0)
                shows = local;
        }
        if (ask.TravelMiles is int showMiles and > 0 && geo is { } showOrigin)
        {
            var nearShows = shows.Where(e =>
            {
                var lat = e.Venue?.Latitude;
                var lng = e.Venue?.Longitude;
                if (lat is null || lng is null)
                    return true;
                return ShopCopy.MilesBetween((double)lat.Value, (double)lng.Value, showOrigin.Lat, showOrigin.Lng) <= showMiles;
            }).ToList();
            if (nearShows.Count > 0)
                shows = nearShows;
        }
        shows = shows.Take(8).ToList();

        var mfrQ = _db.Manufacturers.AsNoTracking().Where(m => m.IsActive);
        if (ask.Interests.Contains("pokemon"))
            mfrQ = mfrQ.Where(m => m.Name.Contains("Pokémon") || m.Name.Contains("Pokemon") || m.Name.Contains("The Pokémon Company"));
        else if (ask.Interests.Contains("magic"))
            mfrQ = mfrQ.Where(m => m.Name.Contains("Wizards") || m.Name.Contains("Magic"));
        else if (ask.Interests.Contains("sports"))
            mfrQ = mfrQ.Where(m => m.Name.Contains("Topps") || m.Name.Contains("Panini") || m.Name.Contains("Upper Deck") || m.Name.Contains("Bowman"));
        var mfrs = await mfrQ.OrderBy(m => m.Name).Take(8).ToListAsync(ct);

        var answer = new AgentAnswer
        {
            PiiStripped = ask.PiiStripped,
            Warnings = ask.Warnings,
            Assistant = ask.Assistant,
            HumanRole = ask.HumanRole,
            Shops = shops.Select(s => (object)new
            {
                s.Name,
                s.City,
                s.StateCode,
                s.PrimarySpecialty,
                url = origin + ShopCopy.Loc(s),
                md = origin + ShopCopy.MdLoc(s)
            }).ToList(),
            Shows = shows.Select(e => (object)new
            {
                e.Title,
                e.StartsAt,
                city = e.City ?? e.Venue?.City,
                e.StateCode,
                url = origin + CalendarController.Loc(e)
            }).ToList(),
            Manufacturers = mfrs.Select(m => (object)new
            {
                m.Name,
                url = origin + "/manufacturers/" + m.Slug
            }).ToList()
        };

        if (ask.HumanRole is null or "collector" or "parent" or "dealer")
        {
            answer.Guides.Add(new { title = "New to Trading Cards", url = origin + "/new-to-trading-cards" });
            if (ask.Interests.Contains("pokemon"))
                answer.Guides.Add(new { title = "Bringing Pokémon Cards to Trade at Gaming Shows", url = origin + "/news/pokemon-trading-at-gaming-shows" });
        }
        AddNextSteps(answer, ask, origin);
        answer.Markdown = RenderMarkdown(ask, shops, shows, mfrs, origin);
        var extra = await _enrich.EnrichMarkdownAsync(ask, answer, ct);
        if (!string.IsNullOrWhiteSpace(extra))
            answer.Markdown += "\n\n" + extra;
        return answer;
    }

    private static void AddNextSteps(AgentAnswer answer, AgentAskResult ask, string origin)
    {
        switch (ask.HumanRole)
        {
            case "shop_owner":
                answer.NextSteps.Add(new { title = "Claim or edit your shop listing", url = origin + "/owner/login" });
                answer.NextSteps.Add(new { title = "Join the Preview list", url = origin + "/signup" });
                break;
            case "promoter":
                answer.NextSteps.Add(new { title = "Submit a card show", url = origin + "/shows/submit" });
                answer.NextSteps.Add(new { title = "National calendar", url = origin + "/calendar" });
                break;
            case "media":
                answer.NextSteps.Add(new { title = "News desk", url = origin + "/news" });
                answer.NextSteps.Add(new { title = "llms.txt", url = origin + "/llms.txt" });
                break;
            default:
                answer.NextSteps.Add(new { title = "Shop directory", url = origin + "/shops" });
                answer.NextSteps.Add(new { title = "Card show calendar", url = origin + "/calendar" });
                if (ask.HumanRole == "parent")
                    answer.NextSteps.Add(new { title = "New to Trading Cards", url = origin + "/new-to-trading-cards" });
                break;
        }
    }

    private static string RenderMarkdown(AgentAskResult ask, List<Shop> shops, List<ShopEvent> shows, List<Manufacturer> mfrs, string origin)
    {
        var sb = new StringBuilder();
        sb.AppendLine(AgentCopy.Welcome);
        sb.AppendLine();
        sb.AppendLine(AgentCopy.Privacy);
        sb.AppendLine();
        var who = ask.HumanRole ?? "visitor";
        var where = string.Join(", ", new[] { ask.City, ask.StateCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
        sb.Append("## For a ").Append(who.Replace('_', ' '));
        if (where.Length > 0)
            sb.Append(" in ").Append(where);
        if (ask.Interests.Count > 0)
            sb.Append(" interested in ").Append(string.Join(", ", ask.Interests));
        sb.AppendLine();
        sb.AppendLine();
        if (shops.Count > 0)
        {
            sb.AppendLine("### Nearby shops (from the NTCA directory)");
            foreach (var s in shops)
                sb.Append("- [").Append(s.Name).Append("](").Append(origin).Append(ShopCopy.Loc(s)).Append(") — ")
                    .Append(s.City).Append(", ").Append(s.StateCode).AppendLine();
            sb.AppendLine();
        }
        else
            sb.AppendLine("No shop listings matched that location yet. Browse the full directory at " + origin + "/shops.\n");
        if (shows.Count > 0)
        {
            sb.AppendLine("### Upcoming card shows");
            foreach (var e in shows)
                sb.Append("- [").Append(e.Title).Append("](").Append(origin).Append(CalendarController.Loc(e)).Append(") — ")
                    .Append(e.StartsAt.ToString("MMM d, yyyy")).Append(" · ")
                    .Append(e.City ?? e.Venue?.City).Append(", ").Append(e.StateCode).AppendLine();
            sb.AppendLine();
        }
        if (mfrs.Count > 0)
        {
            sb.AppendLine("### Manufacturers");
            foreach (var m in mfrs)
                sb.Append("- [").Append(m.Name).Append("](").Append(origin).Append("/manufacturers/").Append(m.Slug).AppendLine(")");
            sb.AppendLine();
        }
        if (ask.HumanRole is "parent" or "collector" or null)
            sb.AppendLine("New collectors: " + origin + "/new-to-trading-cards");
        if (ask.HumanRole == "shop_owner")
            sb.AppendLine("Shop owners can claim a listing at " + origin + "/owner/login");
        if (ask.HumanRole == "promoter")
            sb.AppendLine("Promoters can submit a show at " + origin + "/shows/submit");
        if (!string.IsNullOrWhiteSpace(ask.Budget))
            sb.AppendLine("\nWe do not have priced inventory, so budget was noted but not used to filter listings.");
        return sb.ToString();
    }

    private static DateTime ParseUntil(AgentAskResult ask, DateTime now)
    {
        var raw = (ask.Dates ?? "").ToLowerInvariant();
        if (raw.Contains("weekend") || raw.Contains("7 day"))
            return now.AddDays(8);
        if (raw.Contains("30") || raw.Contains("month"))
            return now.AddDays(31);
        if (raw.Contains("60"))
            return now.AddDays(60);
        if (DateTime.TryParse(raw, out var dt) && dt > now)
            return dt.AddDays(2);
        return now.AddDays(90);
    }

    private static HashSet<string> SpecialtiesFor(List<string> interests)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in interests)
        {
            switch (i)
            {
                case "sports": set.Add("SPORTS"); set.Add("ALL"); break;
                case "pokemon": set.Add("POKEMON"); set.Add("TCG_MIXED"); set.Add("ALL"); break;
                case "magic": set.Add("MAGIC"); set.Add("TCG_MIXED"); set.Add("ALL"); break;
                case "tcg": set.Add("TCG_MIXED"); set.Add("POKEMON"); set.Add("MAGIC"); set.Add("ALL"); break;
                case "nonsport": set.Add("NONSPORT"); set.Add("ALL"); break;
            }
        }
        return set;
    }
}

public readonly record struct AgentVisitHit(string AssistantName, string Role, bool IsVerified, string? PagePath, DateTime CreatedAt);

public sealed class AgentVisitBuffer
{
    private readonly ConcurrentQueue<AgentVisitHit> _q = new();
    public void Enqueue(AgentVisitHit hit) => _q.Enqueue(hit);
    public List<AgentVisitHit> Drain(int max = 200)
    {
        var list = new List<AgentVisitHit>();
        while (list.Count < max && _q.TryDequeue(out var h))
            list.Add(h);
        return list;
    }
}

public sealed class AgentVisitFlushService : BackgroundService
{
    private readonly AgentVisitBuffer _buffer;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AgentVisitFlushService> _log;

    public AgentVisitFlushService(AgentVisitBuffer buffer, IServiceScopeFactory scopes, ILogger<AgentVisitFlushService> log)
    {
        _buffer = buffer;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            await FlushAsync(CancellationToken.None);
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        var batch = _buffer.Drain();
        if (batch.Count == 0)
            return;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NtcaDbContext>();
            db.AgentVisits.AddRange(batch.Select(h => new AgentVisit
            {
                AssistantName = h.AssistantName,
                Role = h.Role,
                IsVerified = h.IsVerified,
                PagePath = h.PagePath,
                CreatedAt = h.CreatedAt
            }));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to flush {Count} agent visits", batch.Count);
        }
    }
}
