using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class PeopleController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IFileStorage _files;

    public PeopleController(NtcaDbContext db, IFileStorage files)
    {
        _db = db;
        _files = files;
    }

    [HttpGet("/people")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var people = await _db.PersonBios.AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);
        var figures = await _db.IndustryFigures.AsNoTracking()
            .Where(f => f.IsPublished)
            .OrderBy(f => f.FullName)
            .ToListAsync(ct);
        ViewData["Title"] = "People";
        ViewData["Description"] = "The people of the National Trading Card Association — officers, first members, and industry figures.";
        ViewData["MainClass"] = "site-main";
        ViewBag.Figures = figures;
        ViewBag.Files = _files;
        return View(people);
    }

    [HttpGet("/people/{slug}")]
    [HttpGet("/about/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var person = await _db.PersonBios.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (person is not null)
        {
            var origin = $"{Request.Scheme}://{Request.Host}";
            var canonical = origin + "/about/" + person.Slug;
            var image = string.IsNullOrWhiteSpace(person.PhotoLegacyPath) ? null : _files.PublicUrl(person.PhotoLegacyPath);
            ViewData["Title"] = person.FullName;
            ViewData["Description"] = PersonJsonLd.FirstParagraph(person.BioText);
            ViewData["MainClass"] = "site-main";
            ViewBag.JsonLd = PersonJsonLd.BuildPerson(person, origin, canonical, image);
            ViewBag.ImageUrl = image;
            ViewBag.Canonical = canonical;
            return View("Person", person);
        }

        var figure = await _db.IndustryFigures.AsNoTracking()
            .Include(f => f.Sources)
            .FirstOrDefaultAsync(f => f.Slug == slug && f.IsPublished, ct);
        if (figure is null)
            return NotFound();
        var figOrigin = $"{Request.Scheme}://{Request.Host}";
        var figCanonical = figOrigin + "/people/" + figure.Slug;
        ViewData["Title"] = figure.FullName;
        ViewData["Description"] = figure.Summary ?? figure.FullName;
        ViewData["MainClass"] = "site-main";
        ViewBag.JsonLd = PersonJsonLd.BuildFigure(figure, figOrigin, figCanonical, null);
        ViewBag.Canonical = figCanonical;
        return View("Figure", figure);
    }
}
