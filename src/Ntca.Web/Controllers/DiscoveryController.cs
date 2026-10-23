using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class DiscoveryController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly SiteModeService _mode;

    public DiscoveryController(NtcaDbContext db, SiteModeService mode)
    {
        _db = db;
        _mode = mode;
    }

    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots(CancellationToken ct)
    {
        var preview = await _mode.IsPreviewAsync(ct);
        var body = preview
            ? "User-agent: *\nDisallow: /\n"
            : "User-agent: *\nAllow: /\nSitemap: https://tradingcardsassociation.com/sitemap.xml\n";
        return Content(body, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/llms.txt")]
    public async Task<IActionResult> Llms(CancellationToken ct)
    {
        var summary = await _db.SiteSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == "Site:LlmsSummary", ct);
        var sb = new StringBuilder();
        sb.AppendLine("# National Trading Card Association");
        sb.AppendLine();
        sb.AppendLine(summary?.SettingValue ?? "The National Trading Card Association supports collectors, hobby shops, and the trading-card industry.");
        sb.AppendLine();
        sb.AppendLine("## Primary pages");
        sb.AppendLine("- [Home](https://tradingcardsassociation.com/)");
        sb.AppendLine("- [Shop directory](https://tradingcardsassociation.com/shops)");
        sb.AppendLine("- [National card show calendar](https://tradingcardsassociation.com/calendar)");
        sb.AppendLine("- [Manufacturers](https://tradingcardsassociation.com/manufacturers)");
        sb.AppendLine("- [News](https://tradingcardsassociation.com/news)");
        sb.AppendLine("- [New to Trading Cards](https://tradingcardsassociation.com/new-to-trading-cards)");
        sb.AppendLine();
        sb.AppendLine("## Shop listings (markdown for LLM crawlers)");
        sb.AppendLine("- [Shop directory (md)](https://tradingcardsassociation.com/shops.md)");
        var shops = await _db.Shops.AsNoTracking()
            .Where(s => s.ListingStatus == "ACTIVE")
            .OrderBy(s => s.StateCode).ThenBy(s => s.Name)
            .Select(s => new { s.Name, s.StateCode, s.City, s.Slug })
            .ToListAsync(ct);
        foreach (var s in shops)
        {
            var city = s.City.ToLower().Replace(" ", "-").Replace(".", "");
            sb.Append("- [").Append(s.Name).Append("](https://tradingcardsassociation.com/shops/")
                .Append(s.StateCode.ToLower()).Append('/').Append(city).Append('/').Append(s.Slug)
                .AppendLine(".md)");
        }
        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var origin = $"{Request.Scheme}://{Request.Host}";
        var urls = new List<string>
        {
            "/", "/shops", "/calendar", "/shows", "/manufacturers", "/news", "/new-to-trading-cards", "/signup"
        };
        urls.Add("/shops.md");
        urls.AddRange(await _db.Shops.AsNoTracking()
            .Where(s => s.ListingStatus == "ACTIVE")
            .Select(s => "/shops/" + s.StateCode.ToLower() + "/" + s.City.ToLower().Replace(" ", "-").Replace(".", "") + "/" + s.Slug)
            .ToListAsync(ct));
        urls.AddRange(await _db.Shops.AsNoTracking()
            .Where(s => s.ListingStatus == "ACTIVE")
            .Select(s => "/shops/" + s.StateCode.ToLower() + "/" + s.City.ToLower().Replace(" ", "-").Replace(".", "") + "/" + s.Slug + ".md")
            .ToListAsync(ct));
        urls.AddRange(await _db.ShopEvents.AsNoTracking()
            .Where(e => e.EventType == "CARD_SHOW" && (e.Status == "PUBLISHED" || e.Status == "POSTPONED") && e.StateCode != null && e.City != null)
            .Select(e => "/shows/" + e.StateCode!.ToLower() + "/" + e.City!.ToLower().Replace(" ", "-").Replace(".", "") + "/" + e.Slug)
            .ToListAsync(ct));
        urls.AddRange(await _db.Manufacturers.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => "/manufacturers/" + m.Slug)
            .ToListAsync(ct));
        urls.AddRange(await _db.Posts.AsNoTracking()
            .Where(p => p.Status == "PUBLISHED")
            .Select(p => "/news/" + p.Slug)
            .ToListAsync(ct));

        var xml = new StringBuilder();
        xml.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        xml.AppendLine(@"<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">");
        foreach (var path in urls.Distinct())
        {
            xml.Append("<url><loc>").Append(origin).Append(path).AppendLine("</loc></url>");
        }
        xml.AppendLine("</urlset>");
        return Content(xml.ToString(), "application/xml", Encoding.UTF8);
    }
}
