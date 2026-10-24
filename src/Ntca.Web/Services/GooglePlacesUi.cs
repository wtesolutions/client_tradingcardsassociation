namespace Ntca.Web.Services;

public static class GooglePlacesUi
{
    public static string StarBar(decimal? rating)
    {
        var n = (int)Math.Clamp((int)Math.Round(rating ?? 0), 0, 5);
        return new string('★', n) + new string('☆', 5 - n);
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return char.ToUpperInvariant(parts[0][0]).ToString();
        return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]));
    }
}
