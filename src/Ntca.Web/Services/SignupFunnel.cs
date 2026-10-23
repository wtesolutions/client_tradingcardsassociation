using System.Text.Json;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class FunnelFlow
{
    public const string CookieName = "ntca.funnel";

    public static readonly (string Role, string Title, string Blurb)[] Roles =
    [
        ("collector", "Collector", "I already buy, trade, or collect cards."),
        ("parent", "Parent / new to cards", "I’m helping a kid — or just getting started."),
        ("shop_owner", "Shop owner", "I run a hobby shop and want it listed."),
        ("promoter", "Show promoter", "I organize card shows."),
        ("dealer", "Dealer / breaker", "I buy, sell, or break cards."),
        ("media", "Media / creator", "I write, film, or cover the hobby.")
    ];

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

    public static string[] Path(string? role) => role switch
    {
        "collector" or "parent" => ["who", "interests", "experience", "zip", "travel", "email", "results"],
        "shop_owner" => ["who", "shop_find", "shop_tier", "email", "results"],
        "promoter" => ["who", "show", "email", "results"],
        "dealer" or "media" => ["who", "interests", "email", "results"],
        _ => ["who"]
    };

    public static string Next(string? role, string step)
    {
        var path = Path(role);
        var i = Array.IndexOf(path, step);
        return i >= 0 && i < path.Length - 1 ? path[i + 1] : path[^1];
    }

    public static string? Back(string? role, string step)
    {
        var path = Path(role);
        var i = Array.IndexOf(path, step);
        return i > 0 ? path[i - 1] : null;
    }

    public static int Progress(string? role, string step)
    {
        var path = Path(role);
        var i = Math.Max(0, Array.IndexOf(path, step));
        return (int)Math.Round(100.0 * (i + 1) / path.Length);
    }

    public static string HobbySegment(string? role) => role switch
    {
        "collector" or "parent" => "Collector",
        "shop_owner" => "Local Card Shop",
        "dealer" => "Dealer",
        "media" => "Content Creator",
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
        a.TryGetValue("interests", out var interests);
        a.TryGetValue("zip", out var zip);
        a.TryGetValue("city", out var city);
        a.TryGetValue("state", out var state);
        a.TryGetValue("travel", out var travel);
        return new AgentAsk
        {
            HumanRole = funnel.HumanRole,
            Location = new AgentLocation { Zip = zip, City = city, State = state },
            Interests = string.IsNullOrWhiteSpace(interests)
                ? null
                : interests.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(),
            Dates = string.IsNullOrWhiteSpace(travel) ? null : travel + " miles"
        };
    }
}
