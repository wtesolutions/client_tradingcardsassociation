using System.Net.Sockets;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Ntca.Web.Services;

public static class DatabaseHint
{
    public static bool IsUnreachable(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is SocketException or TimeoutException)
                return true;
            if (e is SqlException sql)
                return sql.Number is not 207 and not 208 and not 515 and not 547 and not 2627 and not 2601;
            var msg = e.Message;
            if (msg.Contains("network-related", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("could not open a connection to SQL Server", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Login failed", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("error occurred while establishing a connection", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("A network-related or instance-specific error", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("The server was not found", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("timeout period elapsed", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static (string Server, string Database, string User) Parse(string? connectionString)
    {
        var server = "";
        var database = "";
        var user = "";
        if (string.IsNullOrWhiteSpace(connectionString))
            return (server, database, user);
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();
            if (key.Equals("Server", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
                server = val;
            else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
                     || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                database = val;
            else if (key.Equals("User ID", StringComparison.OrdinalIgnoreCase)
                     || key.Equals("UID", StringComparison.OrdinalIgnoreCase))
                user = val;
        }
        return (server, database, user);
    }

    public static string HtmlPage(string? connectionString, Exception ex)
    {
        var (server, database, user) = Parse(connectionString);
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>");
        sb.Append("<title>Database unreachable · NTCA Preview</title>");
        sb.Append("<style>");
        sb.Append("body{margin:0;background:#161826;color:#e9e9ed;font:16px/1.55 Inter,system-ui,sans-serif}");
        sb.Append(".wrap{max-width:40rem;margin:0 auto;padding:48px 20px}");
        sb.Append(".kicker{color:#c4baf5;letter-spacing:.08em;text-transform:uppercase;font-size:12px}");
        sb.Append("h1{font-size:28px;line-height:1.2;margin:8px 0 12px}");
        sb.Append("p{color:#9aa0b5}");
        sb.Append("code,kbd{background:#12131d;border:1px solid rgba(233,233,237,.14);border-radius:8px;padding:2px 8px;color:#e9e9ed}");
        sb.Append("ul{color:#e9e9ed} li{margin:8px 0}");
        sb.Append(".card{background:#232532;border:1px solid rgba(233,233,237,.14);border-radius:14px;padding:16px 18px;margin:20px 0}");
        sb.Append("</style></head><body><div class=\"wrap\">");
        sb.Append("<p class=\"kicker\">Development</p>");
        sb.Append("<h1>Can’t reach the SQL Server</h1>");
        sb.Append("<p>The Preview site loaded, but it could not open the database. This page is shown only in Development — not a raw exception.</p>");
        sb.Append("<div class=\"card\"><strong>What the app is trying</strong><ul>");
        sb.Append("<li>Server <code>").Append(Html(server.Length == 0 ? "(not set)" : server)).Append("</code></li>");
        sb.Append("<li>Database <code>").Append(Html(database.Length == 0 ? "(not set)" : database)).Append("</code></li>");
        sb.Append("<li>Login <code>").Append(Html(user.Length == 0 ? "(not set)" : user)).Append("</code></li>");
        sb.Append("</ul></div>");
        sb.Append("<p><strong>Check</strong></p><ul>");
        sb.Append("<li>VPN / LAN to <code>10.100.1.87</code> (Eric’s SQL box, port 1433).</li>");
        sb.Append("<li><code>dotnet user-secrets</code> — a local <code>ConnectionStrings:DefaultConnection</code> overrides <code>appsettings.Development.json</code>.</li>");
        sb.Append("<li>Database name is <code>wte_NtcaDatabase</code> and the login is <code>ntca_app</code> (DEV ONLY password in Development settings).</li>");
        sb.Append("<li>SQL Server is running and TCP 1433 is allowed.</li>");
        sb.Append("<li>If shops load but <code>/signup</code> fails, run <code>db/ssms/04_onboarding_funnel_SSMS.sql</code> in SSMS (idempotent).</li>");
        sb.Append("</ul>");
        sb.Append("<p class=\"card\"><span class=\"kicker\">Driver message</span><br/>").Append(Html(FirstLine(ex.Message))).Append("</p>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static string FirstLine(string message)
    {
        var n = message.IndexOf('\n');
        var line = n < 0 ? message : message[..n];
        return line.Length > 240 ? line[..240] + "…" : line;
    }

    private static string Html(string value)
        => System.Net.WebUtility.HtmlEncode(value);
}

public sealed class DatabaseUnreachableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<DatabaseUnreachableMiddleware> _log;

    public DatabaseUnreachableMiddleware(
        RequestDelegate next,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<DatabaseUnreachableMiddleware> log)
    {
        _next = next;
        _env = env;
        _config = config;
        _log = log;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (_env.IsDevelopment() && DatabaseHint.IsUnreachable(ex))
        {
            if (context.Response.HasStarted)
                throw;
            _log.LogWarning(ex, "SQL Server unreachable in Development.");
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(DatabaseHint.HtmlPage(_config.GetConnectionString("DefaultConnection"), ex));
        }
    }
}
