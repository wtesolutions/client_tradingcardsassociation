using System.Globalization;
using System.Net;
using System.Text;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class CalendarIcs
{
    public const string ProdId = "-//NTCA//Card Show Calendar//EN";
    public const string PublicHost = "https://tradingcardsassociation.com";

    public static string Feed(IEnumerable<ShopEvent> events, string calendarName, string origin)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:" + ProdId);
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        Fold(sb, "X-WR-CALNAME:" + EscapeText(calendarName));
        foreach (var ev in events)
            AppendEvent(sb, ev, origin);
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    public static string GoogleUrl(ShopEvent ev, string origin)
    {
        var (start, end) = DateRange(ev);
        var query = new Dictionary<string, string?>
        {
            ["action"] = "TEMPLATE",
            ["text"] = ev.Title,
            ["dates"] = start.ToString("yyyyMMdd") + "/" + end.ToString("yyyyMMdd"),
            ["details"] = ev.Description,
            ["location"] = Location(ev),
        };
        return "https://calendar.google.com/calendar/render?" + Query(query);
    }

    public static string OutlookUrl(ShopEvent ev)
    {
        var (start, end) = DateRange(ev);
        var query = new Dictionary<string, string?>
        {
            ["rru"] = "addevent",
            ["subject"] = ev.Title,
            ["startdt"] = start.ToString("yyyy-MM-dd"),
            ["enddt"] = end.ToString("yyyy-MM-dd"),
            ["body"] = ev.Description,
            ["location"] = Location(ev),
            ["path"] = "/calendar/action/compose",
        };
        return "https://outlook.live.com/calendar/0/deeplink/compose?" + Query(query);
    }

    public static string IcsPath(ShopEvent ev)
    {
        var loc = Ntca.Web.Controllers.CalendarController.Loc(ev);
        return loc + ".ics";
    }

    public static string Location(ShopEvent ev)
    {
        var parts = new[]
        {
            ev.Venue?.Name,
            ev.Venue?.Address1,
            ev.City ?? ev.Venue?.City,
            ev.StateCode ?? ev.Venue?.StateCode
        };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static (DateTime Start, DateTime EndExclusive) DateRange(ShopEvent ev)
    {
        var start = ev.StartsAt.Date;
        var last = (ev.EndsAt ?? ev.StartsAt).Date;
        if (last < start)
            last = start;
        return (start, last.AddDays(1));
    }

    private static void AppendEvent(StringBuilder sb, ShopEvent ev, string origin)
    {
        var (start, end) = DateRange(ev);
        var loc = Ntca.Web.Controllers.CalendarController.Loc(ev);
        sb.AppendLine("BEGIN:VEVENT");
        Fold(sb, "UID:" + ev.PublicId.ToString("D") + "@tradingcardsassociation.com");
        sb.AppendLine("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
        sb.AppendLine("DTSTART;VALUE=DATE:" + start.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        sb.AppendLine("DTEND;VALUE=DATE:" + end.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        Fold(sb, "SUMMARY:" + EscapeText(ev.Title));
        if (!string.IsNullOrWhiteSpace(ev.Description))
            Fold(sb, "DESCRIPTION:" + EscapeText(ev.Description));
        var place = Location(ev);
        if (!string.IsNullOrWhiteSpace(place))
            Fold(sb, "LOCATION:" + EscapeText(place));
        Fold(sb, "URL:" + origin.TrimEnd('/') + loc);
        sb.AppendLine("STATUS:" + (ev.Status == "CANCELED" ? "CANCELLED" : ev.Status == "POSTPONED" ? "TENTATIVE" : "CONFIRMED"));
        sb.AppendLine("END:VEVENT");
    }

    private static void Fold(StringBuilder sb, string line)
    {
        if (line.Length <= 74)
        {
            sb.AppendLine(line);
            return;
        }
        sb.AppendLine(line[..74]);
        for (var i = 74; i < line.Length; i += 73)
        {
            var take = Math.Min(73, line.Length - i);
            sb.Append(' ').AppendLine(line.Substring(i, take));
        }
    }

    private static string EscapeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return text.Replace(@"\", @"\\").Replace(";", @"\;").Replace(",", @"\,").Replace("\r\n", @"\n").Replace("\n", @"\n");
    }

    private static string Query(Dictionary<string, string?> pairs)
        => string.Join("&", pairs
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value)));
}
