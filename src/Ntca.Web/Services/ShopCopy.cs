using System.Globalization;
using System.Text;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class ShopCopy
{
    public static readonly IReadOnlyDictionary<string, string> StateNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
        ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
        ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii", ["ID"] = "Idaho",
        ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
        ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland",
        ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi",
        ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada",
        ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico", ["NY"] = "New York",
        ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
        ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina",
        ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah",
        ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "West Virginia",
        ["WI"] = "Wisconsin", ["WY"] = "Wyoming", ["DC"] = "District of Columbia"
    };

    public static string StateName(string? code)
        => string.IsNullOrWhiteSpace(code) ? "" : (StateNames.TryGetValue(code.Trim(), out var n) ? n : code.Trim().ToUpperInvariant());

    public static string CitySlug(string city)
        => city.ToLowerInvariant().Replace(' ', '-').Replace(".", "");

    public static string Loc(Shop shop)
        => $"/shops/{shop.StateCode.ToLowerInvariant()}/{CitySlug(shop.City)}/{shop.Slug}";

    public static string MdLoc(Shop shop) => Loc(shop) + ".md";

    public static string StoreType(Shop shop)
    {
        var spec = shop.PrimarySpecialty?.ToUpperInvariant();
        if (spec == "SPORTS" || Contains(shop.Name, "sportscard", "sports card"))
            return "SportingGoodsStore";
        if (spec is "POKEMON" or "MAGIC" or "TCG_MIXED" or "NONSPORT"
            || Contains(shop.Name, "hobby", "tcg", "pokemon", "game"))
            return "HobbyShop";
        return "Store";
    }

    public static IReadOnlyList<string> KnowsAbout(string? specialty)
    {
        return specialty?.ToUpperInvariant() switch
        {
            "SPORTS" => ["sports cards", "trading cards"],
            "POKEMON" => ["Pokémon TCG", "trading cards"],
            "MAGIC" => ["Magic: The Gathering", "trading cards"],
            "TCG_MIXED" => ["trading card games", "Pokémon TCG", "sports cards"],
            "NONSPORT" => ["non-sport trading cards", "trading cards"],
            "ALL" => ["sports cards", "Pokémon TCG", "Magic: The Gathering", "trading cards"],
            _ => ["trading cards"]
        };
    }

    public static string KindPhrase(Shop shop)
    {
        var spec = shop.PrimarySpecialty?.ToUpperInvariant();
        if (spec == "SPORTS" || Contains(shop.Name, "sportscard", "sports card"))
            return "sports card shop";
        if (spec == "POKEMON")
            return "Pokémon TCG shop";
        return "trading card shop";
    }

    public static string? SpecialtySentence(string? specialty)
    {
        return specialty?.ToUpperInvariant() switch
        {
            "SPORTS" => "They specialize in sports cards.",
            "POKEMON" => "They specialize in Pokémon TCG.",
            "MAGIC" => "They specialize in Magic: The Gathering.",
            "TCG_MIXED" => "They specialize in mixed TCG and sports-card inventory.",
            "NONSPORT" => "They specialize in non-sport trading cards.",
            "ALL" => "They carry sports cards, Pokémon TCG, and other trading card games.",
            _ => null
        };
    }

    public static string Summary(Shop shop, IReadOnlyList<ShopHours>? hours = null)
    {
        var sb = new StringBuilder();
        sb.Append(shop.Name)
            .Append(" is a ")
            .Append(KindPhrase(shop))
            .Append(" in ")
            .Append(shop.City)
            .Append(", ")
            .Append(StateName(shop.StateCode))
            .Append('.');
        var spec = SpecialtySentence(shop.PrimarySpecialty);
        if (spec != null)
            sb.Append(' ').Append(spec);
        var hour = HoursSentence(hours);
        if (hour != null)
            sb.Append(' ').Append(hour);
        return sb.ToString();
    }

    public static string? HoursSentence(IReadOnlyList<ShopHours>? hours)
    {
        if (hours is null || hours.Count == 0)
            return null;
        var open = hours
            .Where(h => !h.IsClosed && h.OpensAt is not null && h.ClosesAt is not null)
            .OrderBy(h => h.DayOfWeek)
            .Select(h => $"{DayName(h.DayOfWeek)} {h.OpensAt:HH\\:mm}–{h.ClosesAt:HH\\:mm}")
            .ToList();
        return open.Count == 0 ? null : "Hours: " + string.Join(", ", open) + ".";
    }

    public static string DayName(byte dow) => dow switch
    {
        0 => "Sunday",
        1 => "Monday",
        2 => "Tuesday",
        3 => "Wednesday",
        4 => "Thursday",
        5 => "Friday",
        6 => "Saturday",
        _ => "Day"
    };

    public static string SchemaDay(byte dow) => "https://schema.org/" + DayName(dow);

    public static string InstagramUrl(string handle)
    {
        var h = handle.Trim().TrimStart('@');
        return "https://www.instagram.com/" + h + "/";
    }

    public static string MapsUrl(Shop shop)
    {
        if (shop.Latitude is { } lat && shop.Longitude is { } lng)
            return "https://www.google.com/maps/search/?api=1&query=" +
                   lat.ToString(CultureInfo.InvariantCulture) + "," + lng.ToString(CultureInfo.InvariantCulture);
        var q = string.Join(" ", new[] { shop.AddressLine1, shop.City, shop.StateCode, shop.PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        return "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(q);
    }

    public static bool HasMappableAddress(Shop shop)
        => shop.Latitude is not null && shop.Longitude is not null
           || !string.IsNullOrWhiteSpace(shop.AddressLine1)
           || !string.IsNullOrWhiteSpace(shop.City);

    private static bool Contains(string hay, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (hay.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
