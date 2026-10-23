using System.Text.Json;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class FunnelFlow
{
    public const string CookieName = "ntca.funnel";

    public const string StageRegister = "REGISTER";
    public const string StageOnboarding = "ONBOARDING";

    public const string StatusInProgress = "IN_PROGRESS";
    public const string StatusRegistered = "REGISTERED";
    public const string StatusConfirmed = "CONFIRMED";
    public const string StatusOnboarding = "ONBOARDING";
    public const string StatusCompleted = "COMPLETED";
    public const string StatusSkipped = "SKIPPED";
    public const string StatusAbandoned = "ABANDONED";

    public const string OnboardNotStarted = "NOT_STARTED";
    public const string OnboardInProgress = "IN_PROGRESS";
    public const string OnboardCompleted = "COMPLETED";
    public const string OnboardSkipped = "SKIPPED";

    public static readonly (string Role, string Title, string Blurb)[] Roles =
    [
        ("collector", "Collector", "I already buy, trade, or collect cards."),
        ("parent", "Parent / new to cards", "I’m helping a kid — or just getting started."),
        ("shop_owner", "Shop owner", "I run a hobby shop and want it listed."),
        ("promoter", "Show promoter", "I organize card shows."),
        ("dealer", "Dealer / breaker", "I buy, sell, or break cards."),
        ("media", "Media / creator", "I write, film, or cover the hobby."),
        ("manufacturer", "Manufacturer", "I publish or make trading cards.")
    ];

    public static readonly string[] RoleOrder =
        ["collector", "parent", "dealer", "media", "shop_owner", "promoter", "manufacturer"];

    public static readonly (string Key, string Label)[] CollectInterests =
    [
        ("sports", "Sports"),
        ("baseball", "Baseball"),
        ("football", "Football"),
        ("basketball", "Basketball"),
        ("pokemon", "Pokémon / TCG"),
        ("nonsport", "Non-sport"),
        ("vintage", "Vintage"),
        ("graded", "Graded")
    ];

    public static readonly string[] RegisterSteps = ["register", "thanks"];
    public static readonly string[] OnboardingReportSteps =
        ["who", "zip", "interests", "experience", "travel", "shop_find", "shop_tier", "show_find", "mfr_find", "done"];

    public static string[] RoleSteps(string role) => role switch
    {
        "collector" or "parent" => ["zip", "interests", "experience", "travel"],
        "shop_owner" => ["shop_find", "shop_tier"],
        "promoter" => ["show_find"],
        "dealer" or "media" => ["interests"],
        "manufacturer" => ["mfr_find"],
        _ => []
    };

    public static IReadOnlyList<string> NormalizeRoles(IEnumerable<string?>? roles)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roles is not null)
        {
            foreach (var raw in roles)
            {
                var role = AgentPrivacy.NormalizeRole(raw);
                if (role is not null)
                    set.Add(role);
            }
        }
        return RoleOrder.Where(set.Contains).ToArray();
    }

    public static IReadOnlyList<string> ReadRoles(SignupFunnel funnel)
    {
        var fromJson = ReadRolesJson(funnel.RolesJson);
        if (fromJson.Count > 0)
            return fromJson;
        var answers = ReadAnswers(funnel.AnswersJson);
        if (answers.TryGetValue("roles", out var csv) && !string.IsNullOrWhiteSpace(csv))
            return NormalizeRoles(csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        return NormalizeRoles(funnel.HumanRole is null ? [] : [funnel.HumanRole]);
    }

    public static IReadOnlyList<string> ReadRolesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return NormalizeRoles(list);
        }
        catch
        {
            return NormalizeRoles(json.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }
    }

    public static string WriteRoles(IEnumerable<string> roles)
        => JsonSerializer.Serialize(NormalizeRoles(roles));

    public static string[] OnboardingPath(IEnumerable<string?>? roles)
    {
        var selected = NormalizeRoles(roles);
        var steps = new List<string> { "who" };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in selected)
        {
            foreach (var step in RoleSteps(role))
            {
                if (seen.Add(step))
                    steps.Add(step);
            }
        }
        steps.Add("done");
        return [.. steps];
    }

    public static string[] Path(string? role) => OnboardingPath(role is null ? [] : [role]);

    public static string Next(IReadOnlyList<string> roles, string step)
    {
        var path = OnboardingPath(roles);
        var i = Array.IndexOf(path, step);
        return i >= 0 && i < path.Length - 1 ? path[i + 1] : path[^1];
    }

    public static string Next(string? role, string step) => Next(NormalizeRoles(role is null ? [] : [role]), step);

    public static string? Back(IReadOnlyList<string> roles, string step)
    {
        var path = OnboardingPath(roles);
        var i = Array.IndexOf(path, step);
        return i > 0 ? path[i - 1] : null;
    }

    public static string? Back(string? role, string step) => Back(NormalizeRoles(role is null ? [] : [role]), step);

    public static int Progress(IReadOnlyList<string> roles, string step)
    {
        var path = roles.Count == 0 ? OnboardingPath(["collector"]) : OnboardingPath(roles);
        var i = Array.IndexOf(path, step);
        if (i < 0)
            i = 0;
        return (int)Math.Round(100.0 * (i + 1) / path.Length);
    }

    public static int Progress(string? role, string step)
        => Progress(NormalizeRoles(role is null ? [] : [role]), step);

    public static string? RoleForStep(IReadOnlyList<string> roles, string step)
    {
        if (step is "who" or "done" or "register" or "thanks" or "confirm")
            return null;
        foreach (var role in NormalizeRoles(roles))
        {
            if (RoleSteps(role).Contains(step))
                return role;
        }
        return null;
    }

    public static string HobbySegment(string? role) => role switch
    {
        "collector" or "parent" => "Collector",
        "shop_owner" => "Local Card Shop",
        "dealer" => "Dealer",
        "media" => "Content Creator",
        "manufacturer" => "Manufacturer",
        "promoter" => "Other",
        _ => "Other"
    };

    public static Dictionary<string, string> ReadAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static string WriteAnswers(Dictionary<string, string> answers)
        => JsonSerializer.Serialize(answers);

    public static AgentAsk ToAsk(SignupFunnel funnel)
    {
        var a = ReadAnswers(funnel.AnswersJson);
        var roles = ReadRoles(funnel);
        a.TryGetValue("interests", out var interests);
        a.TryGetValue("zip", out var zip);
        a.TryGetValue("city", out var city);
        a.TryGetValue("state", out var state);
        a.TryGetValue("travel", out var travel);
        return new AgentAsk
        {
            HumanRole = roles.FirstOrDefault() ?? funnel.HumanRole,
            Location = new AgentLocation { Zip = zip, City = city, State = state },
            Interests = string.IsNullOrWhiteSpace(interests)
                ? null
                : interests.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            Dates = string.IsNullOrWhiteSpace(travel) ? null : travel + " miles",
            TravelMiles = int.TryParse(travel, out var miles) ? miles : null
        };
    }

    public static (string First, string? Last) SplitName(string? name)
    {
        var t = (name ?? "").Trim();
        if (t.Length == 0)
            return ("", null);
        var space = t.IndexOf(' ');
        if (space < 0)
            return (t.Length <= 80 ? t : t[..80], null);
        var first = t[..space];
        var last = t[(space + 1)..].Trim();
        if (first.Length > 80)
            first = first[..80];
        if (last.Length > 80)
            last = last[..80];
        return (first, string.IsNullOrWhiteSpace(last) ? null : last);
    }

    public static string Hit(object row, string property)
    {
        var el = JsonSerializer.SerializeToElement(row);
        if (el.TryGetProperty(property, out var p))
            return p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.ToString();
        if (property.Length > 0)
        {
            var camel = char.ToLowerInvariant(property[0]) + property[1..];
            if (el.TryGetProperty(camel, out var p2))
                return p2.ValueKind == JsonValueKind.String ? p2.GetString() ?? "" : p2.ToString();
        }
        return "";
    }

    public static string Icon(string key) => key switch
    {
        "collector" => "M7 6h10v14H7z M10 10h4 M10 14h3",
        "parent" => "M8 8a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z M4 20v-2a3 3 0 0 1 3-3h2 M16 9a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z M12 20v-2a4 4 0 0 1 4-4h2",
        "shop_owner" => "M4 10l8-5 8 5 M5 10v10h14V10 M9 20v-6h6v6",
        "promoter" => "M6 5h12v14H6z M6 9h12 M10 5v4 M14 5v4 M9 13h6",
        "dealer" => "M4 9h16v10H4z M8 9V7h8v2 M8 14h8",
        "media" => "M4 8h11v10H4z M15 11l5-3v10l-5-3z",
        "manufacturer" => "M4 20V10l4 3V10l4 3V8l8 4v8z M8 20v-4h4v4",
        "sports" => "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M4.5 10h15 M4.5 14h15",
        "baseball" => "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M8 6c2 3 2 9 0 12 M16 6c-2 3-2 9 0 12",
        "football" => "M4 12c2-6 14-6 16 0  -2 6-14 6-16 0z M8 9l8 6 M16 9l-8 6",
        "basketball" => "M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z M12 4v16 M4 12h16",
        "pokemon" => "M12 5a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M5 12h14 M12 12a2 2 0 1 0 0 0.1",
        "nonsport" => "M7 5h10v14H7z M10 9h4 M10 13h4",
        "vintage" => "M12 6a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M12 10v4l3 2",
        "graded" => "M8 5h8l2 4H6l2-4z M8 9v11h8V9",
        "new" => "M12 20V8 M8 12l4-4 4 4 M7 20h10",
        "year" => "M6 6h12v13H6z M6 10h12 M9 6V4 M15 6V4",
        "vet" => "M12 4l2.4 4.8 5.3.8-3.8 3.7.9 5.3L12 16.4 7.2 18.6l.9-5.3L4.3 9.6l5.3-.8z",
        "15" or "30" or "60" or "120" => "M12 21s7-5.4 7-11a7 7 0 1 0-14 0c0 5.6 7 11 7 11z M12 11a2 2 0 1 0 0.1 0",
        "FREE" => "M5 8h14v10H5z M8 12h8",
        "PAID" => "M8 7h8v3H8z M6 10h12v9H6z",
        "PREMIUM" => "M5 10l3.5-4 3.5 3 3.5-3 3.5 4-2 8H7z",
        "sms" => "M5 6h14v10H9l-4 4z",
        _ => "M6 6h12v12H6z"
    };
}
