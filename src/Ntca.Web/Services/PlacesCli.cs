using Microsoft.Extensions.Options;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class PlacesCli
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken ct = default)
    {
        var verb = args.Length > 1 ? args[1].ToLowerInvariant() : "help";
        if (verb is "help" or "-h" or "--help")
            return Help();

        using var scope = services.CreateScope();
        var enrich = scope.ServiceProvider.GetRequiredService<PlacesEnrich>();
        var sync = scope.ServiceProvider.GetRequiredService<GooglePlacesSync>();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<GooglePlacesOptions>>().Value;
        var dry = args.Any(a => a is "--dry-run" or "-n");
        var limit = ParseInt(args, "--limit");
        var top = ParseInt(args, "--top");
        var maxCalls = ParseInt(args, "--max-calls");
        var rateMs = ParseInt(args, "--rate-ms") ?? PlacesEnrich.DefaultRateMs;
        var shop = ParseLong(args, "--shop");
        var skipWebsite = args.Any(a => a is "--skip-website");
        var force = args.Any(a => a is "--force");
        var cities = ParseString(args, "--cities");
        var export = ParseString(args, "--export");

        switch (verb)
        {
            case "enrich":
                var result = await enrich.EnrichAsync(
                    dry, limit, maxCalls, rateMs, skipWebsite, shop, force, ct, top, cities, export);
                Console.WriteLine(result.Message);
                Console.WriteLine(
                    $"plan shops={result.Plan.ShopsTotal} withPlaceId={result.Plan.WithPlaceId} needMatch={result.Plan.NeedMatch} "
                    + $"needDetails={result.Plan.NeedDetails} needWebsite={result.Plan.NeedWebsite} "
                    + $"calls textSearch={result.Plan.TextSearch} details={result.Plan.PlaceDetails} photos={result.Plan.PlacePhotos} "
                    + $"used={result.CallsUsed} dryRun={result.DryRun} noOp={result.NoOp} hitLimit={result.HitLimit}");
                return result.NoOp ? 2 : 0;
            case "refresh":
                var refresh = await sync.RefreshAsync(dry, shop is { } id ? [id] : null, limit, ct);
                Console.WriteLine(refresh.Message);
                Console.WriteLine(
                    $"plan details={refresh.Planned.PlaceDetails} photos={refresh.Planned.PlacePhotos} shops={refresh.Planned.Shops} "
                    + $"used={refresh.CallsUsed} dryRun={refresh.DryRun} noOp={refresh.NoOp} hitLimit={refresh.HitLimit}");
                return refresh.NoOp ? 2 : 0;
            case "status":
                return await StatusAsync(sync, enrich, opt, ct);
            case "discover":
                Console.WriteLine("places discover is not in this pass. Enrich existing shops first (places enrich). New-shop discovery is a follow-up.");
                return 2;
            default:
                return Help();
        }
    }

    private static async Task<int> StatusAsync(
        GooglePlacesSync sync, PlacesEnrich enrich, GooglePlacesOptions opt, CancellationToken ct)
    {
        var usage = await sync.UsageAsync(ct);
        var match = await sync.StatusAsync(ct);
        var plan = await enrich.StatusPlanAsync(ct);
        Console.WriteLine(
            $"shops total={plan.ShopsTotal} withPlaceId={plan.WithPlaceId} needMatch={plan.NeedMatch} "
            + $"needDetails={plan.NeedDetails} needWebsite={plan.NeedWebsite}");
        Console.WriteLine(
            $"matches unmatched={match.Unmatched} pendingReview={match.Pending} matched={match.Matched} rejected={match.Rejected} "
            + $"lastRefresh={match.LastRefresh:u}");
        Console.WriteLine(
            $"calls today={usage.Today}/{opt.DailyCallLimit} month={usage.Month}/{opt.MonthlyCallLimit} remainingToday={Math.Max(0, opt.DailyCallLimit - usage.Today)}");
        Console.WriteLine(PlacesEnrich.FormatDryRun(plan));
        return 0;
    }

    public static int Help()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  places enrich [--dry-run] [--top N] [--limit N] [--export ranked.csv] [--cities path] [--max-calls N] [--rate-ms 1500] [--skip-website] [--shop ID] [--force]");
        Console.WriteLine("  places refresh [--dry-run] [--limit N] [--shop ID]");
        Console.WriteLine("  places status");
        Console.WriteLine("Local CLI only. Key: GooglePlaces:ApiKey (user-secrets or GooglePlaces__ApiKey). Never committed.");
        return 2;
    }

    public static int? ParseInt(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var n))
                return n;
        }
        return null;
    }

    public static string? ParseString(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    public static long? ParseLong(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(args[i + 1], out var n))
                return n;
        }
        return null;
    }
}
