using Ntca.Core;

namespace Ntca.Web.Services;

public static class EmailCli
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken ct = default)
    {
        var verb = args.Length > 1 ? args[1].ToLowerInvariant() : "help";
        if (verb is not "test")
            return Help();

        var to = ParseArg(args, "--to");
        if (string.IsNullOrWhiteSpace(to))
        {
            Console.Error.WriteLine("email test requires --to <addr>");
            return Help();
        }

        using var scope = services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IAppEmailSender>();
        var result = await sender.SendAsync(
            to.Trim(),
            "NTCA email test",
            "This is a test message from the Ntca.Web email CLI.\n\nFrom: National Trading Card Association <reports@wte.net>\n",
            ct: ct);

        if (result.Delivered)
        {
            Console.WriteLine($"OK  channel={result.Channel}  to={to.Trim()}");
            return 0;
        }

        Console.Error.WriteLine($"FAIL  channel={result.Channel}  {result.UserMessage}");
        return 1;
    }

    public static int Help()
    {
        Console.WriteLine("Usage: email test --to <addr>");
        Console.WriteLine("Sends one test message from Email:FromAddress via SMTP (or Injection API / file sink).");
        return 2;
    }

    public static string? ParseArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
