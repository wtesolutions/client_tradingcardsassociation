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

    public static bool ShowListingBadge(string? tierName)
    {
        if (string.IsNullOrWhiteSpace(tierName))
            return false;
        var t = tierName.Trim();
        return !t.Equals("FREE", StringComparison.OrdinalIgnoreCase)
               && !t.Equals("Free", StringComparison.OrdinalIgnoreCase)
               && !t.Equals("Free Listing", StringComparison.OrdinalIgnoreCase);
    }

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

    public static string SpecialtyLabel(string? specialty) => specialty?.ToUpperInvariant() switch
    {
        "SPORTS" => "Sports",
        "POKEMON" => "Pokémon TCG",
        "MAGIC" => "Magic",
        "TCG_MIXED" => "Mixed TCG",
        "NONSPORT" => "Non-sport",
        "ALL" => "All cards",
        _ => ""
    };

    public static string OneLine(Shop shop)
    {
        if (!string.IsNullOrWhiteSpace(shop.Description))
        {
            var t = shop.Description.Trim().Replace('\n', ' ');
            return t.Length <= 140 ? t : t[..137] + "…";
        }
        return Summary(shop);
    }

    public static string? MapThumbUrl(Shop shop, int zoom = 15)
    {
        if (shop.Latitude is not { } lat || shop.Longitude is not { } lng)
            return null;
        var la = lat.ToString(CultureInfo.InvariantCulture);
        var ln = lng.ToString(CultureInfo.InvariantCulture);
        return "https://staticmap.openstreetmap.de/staticmap.php?center=" + la + "," + ln
               + "&zoom=" + zoom + "&size=280x280&maptype=mapnik&markers=" + la + "," + ln + ",ol-marker";
    }

    public static string? MapTileUrl(Shop shop, int zoom = 15)
    {
        if (shop.Latitude is not { } lat || shop.Longitude is not { } lng)
            return null;
        var n = 1 << zoom;
        var latRad = (double)lat * Math.PI / 180.0;
        var x = (int)Math.Floor(n * (((double)lng + 180.0) / 360.0));
        var y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        x = Math.Clamp(x, 0, n - 1);
        y = Math.Clamp(y, 0, n - 1);
        return $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
    }

    public static double MilesBetween(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 3958.8;
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static double? MilesFrom(Shop shop, double? originLat, double? originLng)
    {
        if (originLat is null || originLng is null || shop.Latitude is null || shop.Longitude is null)
            return null;
        return MilesBetween(originLat.Value, originLng.Value, (double)shop.Latitude.Value, (double)shop.Longitude.Value);
    }

    public static string FormatMiles(double? miles)
    {
        if (miles is null)
            return "";
        if (miles.Value < 0.05)
            return "0 mi";
        return miles.Value < 10
            ? miles.Value.ToString("0.0", CultureInfo.InvariantCulture) + " mi"
            : Math.Round(miles.Value).ToString("0", CultureInfo.InvariantCulture) + " mi";
    }

    public static (double Lat, double Lng)? OriginFromZip(IEnumerable<Shop> shops, string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return null;
        var digits = new string(zip.Where(char.IsDigit).ToArray());
        if (digits.Length < 3)
            return null;
        foreach (var len in new[] { Math.Min(5, digits.Length), 3 })
        {
            var prefix = digits[..len];
            var hit = shops.FirstOrDefault(s =>
                s.PostalCode != null && s.PostalCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && s.Latitude is not null && s.Longitude is not null);
            if (hit?.Latitude is { } lat && hit.Longitude is { } lng)
                return ((double)lat, (double)lng);
        }
        return null;
    }

    public static (double Lat, double Lng)? OriginFromAsk(IEnumerable<Shop> shops, string? zip, string? city)
    {
        var fromZip = OriginFromZip(shops, zip);
        if (fromZip is not null)
            return fromZip;
        if (!string.IsNullOrWhiteSpace(city))
        {
            var hit = shops.FirstOrDefault(s =>
                s.City.Contains(city, StringComparison.OrdinalIgnoreCase)
                && s.Latitude is not null && s.Longitude is not null);
            if (hit?.Latitude is { } lat && hit.Longitude is { } lng)
                return ((double)lat, (double)lng);
        }
        return null;
    }

    public static string TelHref(string phone)
    {
        var sb = new StringBuilder("tel:");
        foreach (var c in phone)
        {
            if (char.IsDigit(c) || c == '+')
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static bool IsClaimed(Shop shop)
        => string.Equals(shop.ClaimStatus, "CLAIMED", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> SpecialtyChips(string? specialty)
    {
        var label = SpecialtyLabel(specialty);
        return label.Length == 0 ? [] : [label];
    }

    public static string FormatClock(TimeOnly time)
    {
        var hour = time.Hour % 12;
        if (hour == 0) hour = 12;
        var ampm = time.Hour < 12 ? "AM" : "PM";
        return time.Minute == 0 ? $"{hour} {ampm}" : $"{hour}:{time.Minute:00} {ampm}";
    }

    public static string? TimeZoneIdForState(string? stateCode)
    {
        var st = (stateCode ?? "").Trim().ToUpperInvariant();
        return st switch
        {
            "HI" => "Pacific/Honolulu",
            "AK" => "America/Anchorage",
            "CA" or "WA" or "OR" or "NV" => "America/Los_Angeles",
            "AZ" => "America/Phoenix",
            "CO" or "UT" or "NM" or "WY" or "MT" or "ID" => "America/Denver",
            "ND" or "SD" or "NE" or "KS" or "OK" or "TX" or "MN" or "IA" or "MO"
                or "AR" or "LA" or "WI" or "IL" or "MS" or "AL" or "TN" => "America/Chicago",
            "PR" or "VI" => "America/Puerto_Rico",
            "" => null,
            _ => "America/New_York"
        };
    }

    public static DateTimeOffset LocalNow(string? stateCode, DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var id = TimeZoneIdForState(stateCode);
        if (id is null)
            return now;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            return TimeZoneInfo.ConvertTime(now, tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return now;
        }
        catch (InvalidTimeZoneException)
        {
            return now;
        }
    }

    public static IReadOnlyList<ShopHours> UsableHours(IEnumerable<ShopHours>? hours)
    {
        if (hours is null)
            return [];
        return hours
            .Where(h => !h.IsClosed && h.OpensAt is not null && h.ClosesAt is not null)
            .OrderBy(h => h.DayOfWeek)
            .ToList();
    }

    public static bool HasUsableHours(IEnumerable<ShopHours>? hours)
        => UsableHours(hours).Count > 0;

    public static bool IsOpenNow(IEnumerable<ShopHours>? hours, string? stateCode, DateTimeOffset? utcNow = null)
        => OpeningStatus(hours, stateCode, utcNow)?.IsOpen == true;

    public static OpeningStatusInfo? OpeningStatus(IEnumerable<ShopHours>? hours, string? stateCode, DateTimeOffset? utcNow = null)
    {
        var openDays = UsableHours(hours);
        if (openDays.Count == 0)
            return null;
        var local = LocalNow(stateCode, utcNow);
        var dow = (byte)local.DayOfWeek;
        var today = hours!.Where(h => h.DayOfWeek == dow).ToList();
        if (today.Count == 0)
            return null;
        var clock = TimeOnly.FromTimeSpan(local.TimeOfDay);
        foreach (var row in today)
        {
            if (row.IsClosed || row.OpensAt is null || row.ClosesAt is null)
                continue;
            if (clock >= row.OpensAt && clock < row.ClosesAt)
                return new OpeningStatusInfo(true, "Open until " + FormatClock(row.ClosesAt.Value));
        }
        var nextOpen = today.Where(h => !h.IsClosed && h.OpensAt is not null && clock < h.OpensAt)
            .OrderBy(h => h.OpensAt)
            .FirstOrDefault();
        if (nextOpen?.OpensAt is { } opens)
            return new OpeningStatusInfo(false, "Opens at " + FormatClock(opens));
        return new OpeningStatusInfo(false, "Closed");
    }

    public static string HoursLine(ShopHours row)
    {
        if (row.IsClosed || row.OpensAt is null || row.ClosesAt is null)
            return "Closed";
        return FormatClock(row.OpensAt.Value) + " – " + FormatClock(row.ClosesAt.Value);
    }

    public static string OsmEmbedUrl(Shop shop)
    {
        if (shop.Latitude is not { } lat || shop.Longitude is not { } lng)
            return "";
        var la = (double)lat;
        var ln = (double)lng;
        var minLng = (ln - 0.02).ToString(CultureInfo.InvariantCulture);
        var minLat = (la - 0.015).ToString(CultureInfo.InvariantCulture);
        var maxLng = (ln + 0.02).ToString(CultureInfo.InvariantCulture);
        var maxLat = (la + 0.015).ToString(CultureInfo.InvariantCulture);
        return "https://www.openstreetmap.org/export/embed.html?bbox="
               + minLng + "%2C" + minLat + "%2C" + maxLng + "%2C" + maxLat
               + "&layer=mapnik&marker=" + la.ToString(CultureInfo.InvariantCulture)
               + "%2C" + ln.ToString(CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<string> ServiceLabels(Shop shop, IEnumerable<ShopEvent>? events = null)
    {
        var labels = new List<string>();
        if (events is not null)
        {
            if (events.Any(e => e.EventType == "TRADE_NIGHT"))
                labels.Add("Buy / sell / trade");
            if (events.Any(e => e.EventType == "IN_STORE_PLAY" || e.EventType == "TOURNAMENT"))
                labels.Add("In-store play");
            if (events.Any(e => e.EventType == "BREAK"))
                labels.Add("Live card breaks");
        }
        return labels;
    }

    public static string PublicAddress(Shop shop)
    {
        var parts = new[] { shop.AddressLine1, shop.AddressLine2 }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(", ", parts);
    }

    public static bool HasPreciseDestination(Shop shop)
        => shop.Latitude is not null && shop.Longitude is not null
           || !string.IsNullOrWhiteSpace(shop.AddressLine1);

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

public sealed record OpeningStatusInfo(bool IsOpen, string Label);
