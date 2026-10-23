using Microsoft.AspNetCore.Mvc;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class LegacyRedirectsController : Controller
{
    [HttpGet("/{file}.html")]
    public IActionResult HtmlFile(string file)
    {
        var dest = LegacyUrls.Resolve("/" + file + ".html");
        return dest is null ? NotFound() : RedirectPermanent(dest);
    }

    [HttpGet("/directory")]
    [HttpGet("/dealers")]
    [HttpGet("/retailers")]
    [HttpGet("/home")]
    [HttpGet("/landing")]
    [HttpGet("/members")]
    [HttpGet("/members-area")]
    [HttpGet("/submit-event")]
    [HttpGet("/grading-services")]
    public IActionResult Alias()
    {
        var dest = LegacyUrls.Resolve(Request.Path.Value);
        return dest is null ? NotFound() : RedirectPermanent(dest);
    }
}
