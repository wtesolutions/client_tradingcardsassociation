using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class NewsController : Controller
{
    private readonly NtcaDbContext _db;
    public NewsController(NtcaDbContext db) => _db = db;

    [HttpGet("/news")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var posts = await _db.Posts.AsNoTracking()
            .Where(p => p.Status == "PUBLISHED")
            .OrderByDescending(p => p.PublishedAt)
            .ToListAsync(ct);
        return View(posts);
    }

    [HttpGet("/news/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == "PUBLISHED", ct);
        if (post is null)
            return NotFound();
        return View(post);
    }
}
