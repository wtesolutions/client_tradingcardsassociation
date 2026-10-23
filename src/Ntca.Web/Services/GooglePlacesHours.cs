using System.Text.Json;

namespace Ntca.Web.Services;

public static class GooglePlacesHours
{
    public static IReadOnlyList<string> WeekdayLines(string? hoursJson)
    {
        if (string.IsNullOrWhiteSpace(hoursJson))
            return [];
        try
        {
            using var doc = JsonDocument.Parse(hoursJson);
            if (!doc.RootElement.TryGetProperty("weekdayDescriptions", out var days))
                return [];
            return days.EnumerateArray()
                .Select(d => d.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
