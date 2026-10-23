using Microsoft.Extensions.Options;
using Ntca.Core;

namespace Ntca.Web.Services;

public sealed class GooglePlacesHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly GooglePlacesOptions _opt;
    private readonly ILogger<GooglePlacesHostedService> _log;

    public GooglePlacesHostedService(
        IServiceScopeFactory scopes,
        IOptions<GooglePlacesOptions> opt,
        ILogger<GooglePlacesHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.HasKey || !_opt.Enabled)
            _log.LogInformation("Google Places nightly refresh is idle (no API key or disabled). Expired cache still purges.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilThreeUtc();
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            try
            {
                using var scope = _scopes.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<GooglePlacesSync>();
                var purged = await sync.PurgeExpiredAsync(stoppingToken);
                _log.LogInformation("Google Places expired-cache purge removed {Count} rows.", purged);
                if (!_opt.HasKey || !_opt.Enabled)
                    continue;
                var result = await sync.RefreshAsync(dryRun: false, shopIds: null, take: null, stoppingToken);
                _log.LogInformation("Google Places nightly refresh: {Message} used={Used} limit={Limit}",
                    result.Message, result.CallsUsed, result.HitLimit);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Google Places nightly batch failed; expired cache still attempted next cycle.");
            }
        }
    }

    public static TimeSpan DelayUntilThreeUtc(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var next = now.Date.AddHours(3);
        if (now >= next)
            next = next.AddDays(1);
        return next - now;
    }
}

public static class GooglePlacesCli
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<GooglePlacesSync>();
        var verb = args.Length > 1 ? args[1].ToLowerInvariant() : "help";
        var dry = args.Any(a => a is "--dry-run" or "-n");
        int? take = ParseInt(args, "--take");
        var shop = ParseInt(args, "--shop");
        GooglePlacesRunResult result = verb switch
        {
            "match" => await sync.MatchAsync(dry, take, ct),
            "refresh" => await sync.RefreshAsync(dry, shop is { } id ? [id] : null, take, ct),
            "purge" => new GooglePlacesRunResult(false, false, "Purged " + await sync.PurgeExpiredAsync(ct) + " expired rows.",
                new GooglePlacesPlan(0, 0, 0, 0), 0, 0, 0, 0, 0, 0, false),
            _ => new GooglePlacesRunResult(false, true,
                "Usage: google-places match|refresh|purge [--dry-run] [--take N] [--shop id]",
                new GooglePlacesPlan(0, 0, 0, 0), 0, 0, 0, 0, 0, 0, false)
        };
        Console.WriteLine(result.Message);
        Console.WriteLine(
            $"plan textSearch={result.Planned.TextSearch} details={result.Planned.PlaceDetails} photos={result.Planned.PlacePhotos} shops={result.Planned.Shops} used={result.CallsUsed} dryRun={result.DryRun} noOp={result.NoOp} hitLimit={result.HitLimit}");
        return result.NoOp && verb is "match" or "refresh" ? 2 : 0;
    }

    private static int? ParseInt(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var n))
                return n;
        }
        return null;
    }
}
