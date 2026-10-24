namespace Ntca.Web.Services;

public sealed class UsCitiesLookup
{
    public const string DefaultRelativePath = "data/places/uscities.tsv";

    private readonly Dictionary<(string City, string State), int> _pop = new();

    public int Count => _pop.Count;

    public static UsCitiesLookup Load(string? path = null)
    {
        var file = ResolvePath(path);
        var lookup = new UsCitiesLookup();
        if (file is null || !File.Exists(file))
            return lookup;
        lookup.Read(File.ReadAllText(file));
        return lookup;
    }

    public static string? ResolvePath(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return Path.GetFullPath(path);
        foreach (var root in SearchRoots())
        {
            var candidate = Path.Combine(root, DefaultRelativePath);
            if (File.Exists(candidate))
                return candidate;
        }
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public void Read(string text)
    {
        using var reader = new StringReader(text);
        var header = reader.ReadLine();
        if (header is null)
            return;
        var cols = header.Split('\t');
        var cityIdx = IndexOf(cols, "city");
        var asciiIdx = IndexOf(cols, "city_ascii");
        var stateIdx = IndexOf(cols, "state_id");
        var popIdx = IndexOf(cols, "population");
        if (cityIdx < 0 || stateIdx < 0 || popIdx < 0)
            return;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
                continue;
            var parts = line.Split('\t');
            if (parts.Length <= Math.Max(popIdx, Math.Max(cityIdx, stateIdx)))
                continue;
            var state = NormState(parts[stateIdx]);
            if (state.Length != 2 || !int.TryParse(parts[popIdx], out var pop))
                continue;
            Add(NormCity(parts[cityIdx]), state, pop);
            if (asciiIdx >= 0 && asciiIdx < parts.Length)
                Add(NormCity(parts[asciiIdx]), state, pop);
        }
    }

    public int Population(string? city, string? state)
    {
        var key = (NormCity(city), NormState(state));
        return _pop.TryGetValue(key, out var n) ? n : 0;
    }

    private void Add(string city, string state, int pop)
    {
        if (city.Length == 0)
            return;
        var key = (city, state);
        if (!_pop.TryGetValue(key, out var existing) || pop > existing)
            _pop[key] = pop;
    }

    public static string NormCity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        return string.Join(' ', raw.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormState(string? raw)
        => (raw ?? "").Trim().ToUpperInvariant();

    private static int IndexOf(string[] cols, string name)
    {
        for (var i = 0; i < cols.Length; i++)
        {
            if (cols[i].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static IEnumerable<string> SearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (seen.Add(dir.FullName))
                    yield return dir.FullName;
                dir = dir.Parent;
            }
        }
    }
}
