using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class PagesController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IFileStorage _files;

    public PagesController(NtcaDbContext db, IFileStorage files)
    {
        _db = db;
        _files = files;
    }

    [HttpGet("/network")]
    public IActionResult Network()
    {
        ViewData["Title"] = "The NTCA Network";
        ViewData["Description"] = "Six avenues. One shared future. The Association, Foundation, Convention, Hall of Fame, Advocacy Network, and International TCA.";
        ViewData["MainClass"] = "site-main";
        ViewBag.HallUrl = _files.PublicUrl("assets/network-convention-hall.jpeg");
        ViewBag.ShopUrl = _files.PublicUrl("assets/shop-grey.jpeg");
        return View();
    }

    [HttpGet("/history")]
    public IActionResult History() => View();

    [HttpGet("/contact")]
    public IActionResult Contact()
    {
        ViewData["Title"] = "Contact";
        ViewData["Description"] = "Contact the National Trading Card Association.";
        ViewData["MainClass"] = "site-main";
        return View();
    }

    [HttpGet("/events")]
    public async Task<IActionResult> Events(CancellationToken ct)
    {
        var rows = await _db.AssociationEvents.AsNoTracking()
            .Where(e => e.Status == "PUBLISHED")
            .OrderBy(e => e.StartsAt)
            .ToListAsync(ct);
        ViewData["Title"] = "Events";
        ViewData["Description"] = "The National Trading Card Convention, Hall of Fame induction, and NTCA event calendar.";
        ViewData["MainClass"] = "site-main";
        return View(rows);
    }

    [HttpGet("/bloggers")]
    public async Task<IActionResult> Bloggers(CancellationToken ct)
    {
        var rows = await _db.Bloggers.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync(ct);
        ViewData["Title"] = "Top Trading Card Blogs";
        ViewData["Description"] = "Trading card blogs and publishers followed by the NTCA community.";
        ViewData["MainClass"] = "site-main";
        ViewBag.Files = _files;
        return View(rows);
    }

    [HttpGet("/bloggers/{slug}")]
    public async Task<IActionResult> Blogger(string slug, CancellationToken ct)
    {
        var row = await _db.Bloggers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Slug == slug && b.IsActive, ct);
        if (row is null)
            return NotFound();
        ViewData["Title"] = row.Name;
        ViewData["Description"] = row.Description ?? row.Name;
        ViewData["MainClass"] = "site-main";
        ViewBag.LogoUrl = string.IsNullOrWhiteSpace(row.LogoLegacyPath) ? null : _files.PublicUrl(row.LogoLegacyPath);
        return View(row);
    }

    [HttpGet("/products")]
    public IActionResult Products()
    {
        ViewData["Title"] = "Product Directory";
        ViewData["Description"] = "Find trading card products, brands and supplies through NTCA manufacturers and shops.";
        ViewData["MainClass"] = "site-main";
        return View();
    }

    [HttpGet("/grading")]
    public IActionResult Grading(string? q, string? category)
    {
        var rows = GradingCatalog.All.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
            rows = rows.Where(r => string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            rows = rows.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
                || r.Category.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        ViewData["Title"] = "Grading Services";
        ViewData["Description"] = "Card grading, authentication, autograph certification and submission services.";
        ViewData["MainClass"] = "site-main";
        ViewBag.Q = q ?? "";
        ViewBag.Category = category ?? "";
        ViewBag.Categories = GradingCatalog.All.Select(r => r.Category).Distinct().OrderBy(c => c).ToList();
        return View(rows.ToList());
    }
}
