using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class GalleryController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IFileStorage _files;

    public GalleryController(NtcaDbContext db, IFileStorage files)
    {
        _db = db;
        _files = files;
    }

    [HttpGet("/gallery")]
    public async Task<IActionResult> Index(string? category, CancellationToken ct)
    {
        var items = await _db.GalleryItems.AsNoTracking()
            .Where(g => g.IsPublished)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync(ct);
        var withUrls = items
            .Select(g => (Item: g, Url: _files.PublicUrl(g.LegacyPath), Category: GalleryJsonLd.CategoryOf(g)))
            .ToList();
        var categories = withUrls.Select(x => x.Category).Distinct().OrderBy(c => c).ToList();
        if (!string.IsNullOrWhiteSpace(category))
            withUrls = withUrls.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        var origin = $"{Request.Scheme}://{Request.Host}";
        ViewData["Title"] = "Gallery";
        ViewData["Description"] = "Photos from the trading card community and the National Trading Card Association.";
        ViewData["MainClass"] = "site-main";
        ViewBag.JsonLd = GalleryJsonLd.Build(withUrls.Select(x => (x.Item, x.Url)).ToList(), origin, origin + "/gallery");
        ViewBag.Categories = categories;
        ViewBag.Category = category ?? "";
        return View(withUrls);
    }
}
