using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

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
        var origin = $"{Request.Scheme}://{Request.Host}";
        SiteMeta.Apply(
            this,
            "News from the National Trading Card Association",
            "News and notes from the National Trading Card Association.",
            "/news",
            jsonLd: NewsJsonLd.BuildIndex(origin, posts));
        return View(posts);
    }

    [HttpGet("/news/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var post = await _db.Posts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == "PUBLISHED", ct);
        if (post is null)
            return NotFound();
        var origin = $"{Request.Scheme}://{Request.Host}";
        var canonical = origin + "/news/" + post.Slug;
        var desc = string.IsNullOrWhiteSpace(post.Excerpt) ? post.Title : post.Excerpt;
        SiteMeta.Apply(
            this,
            post.Title,
            desc!,
            "/news/" + post.Slug,
            ogType: "article",
            jsonLd: NewsJsonLd.BuildPost(post, origin, canonical, SiteMeta.Absolute(Request, SiteMeta.DefaultImagePath)));
        return View(post);
    }
}
