using Ntca.Core;

namespace Ntca.Web.Services;

public sealed class FunnelReport
{
    public int Started { get; init; }
    public int Registered { get; init; }
    public int Confirmed { get; init; }
    public int OnboardingStarted { get; init; }
    public int OnboardingCompleted { get; init; }
    public int OnboardingSkipped { get; init; }
    public IReadOnlyList<RoleRow> ByRole { get; init; } = [];
    public IReadOnlyList<NameCount> ByUtm { get; init; } = [];
    public IReadOnlyList<StepRow> RegisterSteps { get; init; } = [];
    public IReadOnlyList<StepRow> OnboardSteps { get; init; } = [];
    public IReadOnlyList<SignupFunnel> Recent { get; init; } = [];

    public sealed record RoleRow(string Name, int Selected, int Onboarded);
    public sealed record NameCount(string Name, int N);
    public sealed record StepRow(string Name, int Enter, int Complete);

    public static bool CountsAsRegistered(string status)
        => status is FunnelFlow.StatusRegistered or FunnelFlow.StatusConfirmed
            or FunnelFlow.StatusOnboarding or FunnelFlow.StatusCompleted or FunnelFlow.StatusSkipped;

    public static bool CountsAsConfirmed(string status)
        => status is FunnelFlow.StatusConfirmed or FunnelFlow.StatusOnboarding
            or FunnelFlow.StatusCompleted or FunnelFlow.StatusSkipped;

    public static FunnelReport Build(IReadOnlyList<SignupFunnel> funnels, IReadOnlyList<SignupStepEvent> events)
    {
        var byRole = FunnelFlow.RoleOrder.Select(role =>
        {
            var rows = funnels.Where(f => FunnelFlow.ReadRoles(f).Contains(role)).ToList();
            return new RoleRow(
                role,
                rows.Count,
                rows.Count(f => f.OnboardingStatus == FunnelFlow.OnboardCompleted));
        }).Where(r => r.Selected > 0).OrderByDescending(r => r.Selected).ToList();

        return new FunnelReport
        {
            Started = funnels.Count,
            Registered = funnels.Count(f => CountsAsRegistered(f.Status) || f.RegisteredAt is not null),
            Confirmed = funnels.Count(f => CountsAsConfirmed(f.Status) || f.ConfirmedAt is not null),
            OnboardingStarted = funnels.Count(f =>
                f.OnboardingStatus is FunnelFlow.OnboardInProgress or FunnelFlow.OnboardCompleted or FunnelFlow.OnboardSkipped
                || f.OnboardingStartedAt is not null),
            OnboardingCompleted = funnels.Count(f => f.OnboardingStatus == FunnelFlow.OnboardCompleted),
            OnboardingSkipped = funnels.Count(f => f.OnboardingStatus == FunnelFlow.OnboardSkipped),
            ByRole = byRole,
            ByUtm = funnels.Where(f => !string.IsNullOrEmpty(f.UtmSource))
                .GroupBy(f => f.UtmSource!)
                .Select(g => new NameCount(g.Key, g.Count()))
                .OrderByDescending(x => x.N)
                .ToList(),
            RegisterSteps = new[] { "register", "thanks", "confirm" }.Select(s => new StepRow(
                s,
                events.Count(e => e.StepKey == s && e.Action == "ENTER"),
                events.Count(e => e.StepKey == s && e.Action == "COMPLETE"))).ToList(),
            OnboardSteps = FunnelFlow.OnboardingReportSteps.Select(s => new StepRow(
                s,
                events.Count(e => e.StepKey == s && e.Action == "ENTER"),
                events.Count(e => e.StepKey == s && e.Action == "COMPLETE"))).ToList(),
            Recent = funnels.OrderByDescending(f => f.UpdatedAt).Take(25).ToList()
        };
    }
}
