using Microsoft.Extensions.Hosting;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class DevLoginBypass
{
    public const string Code = "888888";

    public static bool IsEnabled(IHostEnvironment env, AuthOptions opt)
    {
        if (env.IsProduction())
            return false;
        return env.IsDevelopment() && opt.DevBypassCode;
    }

    public static bool Matches(string? code)
        => string.Equals((code ?? "").Trim(), Code, StringComparison.Ordinal);

    public static bool Accepts(IHostEnvironment env, AuthOptions opt, string? code)
        => IsEnabled(env, opt) && Matches(code);
}
