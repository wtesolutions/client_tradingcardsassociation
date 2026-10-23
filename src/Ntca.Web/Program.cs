using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Auth;
using Ntca.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SocketLabsOptions>(builder.Configuration.GetSection("SocketLabs"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required. Set it with dotnet user-secrets.");

builder.Services.AddDbContext<NtcaDbContext>(opt =>
    opt.UseSqlServer(conn, sql => sql.EnableRetryOnFailure()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ImpressionBuffer>();
builder.Services.AddSingleton<SessionSaltCache>();
builder.Services.AddScoped<IImpressionTracker, ImpressionTracker>();
builder.Services.AddHostedService<ImpressionFlushService>();
builder.Services.AddHostedService<ImpressionRollupService>();
builder.Services.AddScoped<IAppEmailSender, CompositeEmailSender>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<LoginCodeService>();
builder.Services.AddScoped<SiteModeService>();

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
});

builder.Services.AddControllersWithViews();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
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
app.UseAuthentication();
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

public partial class Program { }
