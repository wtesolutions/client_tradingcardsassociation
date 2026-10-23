using System.Security.Claims;
using Amazon.S3;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

if (args.Length > 0 && (
    string.Equals(args[0], "google-places", StringComparison.OrdinalIgnoreCase)
    || string.Equals(args[0], "storage", StringComparison.OrdinalIgnoreCase)
    || string.Equals(args[0], "email", StringComparison.OrdinalIgnoreCase)))
{
    var cli = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = Array.Empty<string>(),
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
    });
    AddNtcaServices(cli);
    var cliApp = cli.Build();
    Environment.ExitCode = args[0].ToLowerInvariant() switch
    {
        "storage" => await Ntca.Web.Services.StorageCli.RunAsync(cliApp.Services, args),
        "email" => await Ntca.Web.Services.EmailCli.RunAsync(cliApp.Services, args),
        _ => await Ntca.Web.Services.GooglePlacesCli.RunAsync(cliApp.Services, args)
    };
    return;
}

var builder = WebApplication.CreateBuilder(args);
AddNtcaServices(builder);

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ImpressionBuffer>();
builder.Services.AddSingleton<SessionSaltCache>();
builder.Services.AddScoped<IImpressionTracker, ImpressionTracker>();
builder.Services.AddHostedService<ImpressionFlushService>();
builder.Services.AddHostedService<ImpressionRollupService>();
builder.Services.AddSingleton<AgentUaRuleCache>();
builder.Services.AddSingleton<AgentVisitBuffer>();
builder.Services.AddScoped<AgentAnswerBuilder>();
builder.Services.AddScoped<IAgentAnswerEnricher, NoOpAgentAnswerEnricher>();
builder.Services.AddHostedService<AgentVisitFlushService>();
builder.Services.AddScoped<LoginCodeService>();
builder.Services.AddScoped<SiteModeService>();
builder.Services.AddScoped<ImpersonationService>();
builder.Services.AddScoped<OwnerShopWork>();
builder.Services.AddScoped<ShopMediaService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("agent-ask", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("directory-search", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = AuthSchemes.Owner;
})
.AddCookie(AuthSchemes.Owner, o =>
{
    o.Cookie.Name = "ntca.owner";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.LoginPath = "/owner/login";
    o.AccessDeniedPath = "/owner/login";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
})
.AddCookie(AuthSchemes.Sadmin, o =>
{
    o.Cookie.Name = "ntca.sadmin";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.LoginPath = "/SADMIN/login";
    o.AccessDeniedPath = "/SADMIN/login";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Owner", p =>
    {
        p.AddAuthenticationSchemes(AuthSchemes.Owner);
        p.RequireAuthenticatedUser();
    });
    options.AddPolicy("Sadmin", p =>
    {
        p.AddAuthenticationSchemes(AuthSchemes.Sadmin);
        p.RequireAuthenticatedUser();
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("SUPERADMIN") || ctx.User.IsInRole("ADMIN") || ctx.User.IsInRole("MODERATOR") ||
            ctx.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "EDITOR"));
    });
    options.AddPolicy("Impersonate", p =>
    {
        p.AddAuthenticationSchemes(AuthSchemes.Sadmin);
        p.RequireAuthenticatedUser();
        p.RequireAssertion(ctx => ctx.User.IsInRole("SUPERADMIN") || ctx.User.IsInRole("ADMIN"));
    });
});

builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();
var authOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;
if (DevLoginBypass.IsEnabled(app.Environment, authOpts))
    app.Logger.LogWarning("DEV login bypass code is active (Auth:DevBypassCode). Never enable this in Production.");

app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
    app.UseMiddleware<DatabaseUnreachableMiddleware>();
app.UseMiddleware<AgentConciergeMiddleware>();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.Use(async (ctx, next) =>
{
    var mode = ctx.RequestServices.GetRequiredService<SiteModeService>();
    if (await mode.IsPreviewAsync(ctx.RequestAborted))
        ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ImpersonationMiddleware>();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllerRoute(
    name: "sadmin",
    pattern: "SADMIN/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Sadmin" });
app.MapControllerRoute(
    name: "owner",
    pattern: "owner/{controller=Shop}/{action=Index}/{id?}",
    defaults: new { area = "Owner" });
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void AddNtcaServices(WebApplicationBuilder builder)
{
    builder.Services.Configure<SocketLabsOptions>(builder.Configuration.GetSection("SocketLabs"));
    builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
    builder.Services.Configure<GooglePlacesOptions>(builder.Configuration.GetSection("GooglePlaces"));
    builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
    builder.Services.AddSingleton<ISmtpTransport, MailKitSmtpTransport>();
    builder.Services.AddScoped<IAppEmailSender, CompositeEmailSender>();
    var conn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required. Set it with dotnet user-secrets.");
    builder.Services.AddDbContext<NtcaDbContext>(opt =>
        opt.UseSqlServer(conn, sql => sql.EnableRetryOnFailure()));
    var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
    if (storage.UseS3 || !string.IsNullOrWhiteSpace(storage.ResolvedBucket))
        builder.Services.AddSingleton<IAmazonS3>(_ => S3ClientFactory.Create(storage));
    if (storage.UseS3)
    {
        builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
    }
    else
    {
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
    }
    builder.Services.AddHttpClient<IGooglePlacesClient, GooglePlacesClient>(client =>
    {
        client.BaseAddress = new Uri("https://places.googleapis.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });
    builder.Services.AddScoped<GooglePlacesSync>();
    builder.Services.AddScoped<ShopSocialEditor>();
    builder.Services.AddHostedService<GooglePlacesHostedService>();
}

public partial class Program { }
