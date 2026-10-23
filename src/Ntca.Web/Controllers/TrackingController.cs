using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;
using Ntca.Web.Services;

namespace Ntca.Web.Controllers;

public sealed class TrackingController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IImpressionTracker _track;

    public TrackingController(NtcaDbContext db, IImpressionTracker track)
    {
        _db = db;
        _track = track;
    }

    [HttpGet("/t/{kind}")]
    public async Task<IActionResult> Click(string kind, long shop, string? net, CancellationToken ct)
    {
        var row = await _db.Shops.AsNoTracking()
            .Include(s => s.Socials)
            .FirstOrDefaultAsync(s => s.ShopId == shop && s.ListingStatus == "ACTIVE", ct);
        if (row is null)
            return NotFound();

        string? dest = kind.ToLowerInvariant() switch
        {
            "website" => ShopSocialLinks.Destination(row, "website", null),
            "phone" when !string.IsNullOrWhiteSpace(row.Phone) => ShopCopy.TelHref(row.Phone),
            "directions" when ShopCopy.HasMappableAddress(row) => ShopCopy.MapsUrl(row),
            "view" => ShopCopy.Loc(row),
            "social" => ShopSocialLinks.Destination(row, "social", net),
            _ => null
        };
        if (dest is null)
            return NotFound();

        var surface = kind.ToLowerInvariant() switch
        {
            "website" => ImpressionSurfaces.ClickWebsite,
            "phone" => ImpressionSurfaces.ClickPhone,
            "directions" => ImpressionSurfaces.ClickDirections,
            "view" => ImpressionSurfaces.ClickView,
            _ => ImpressionSurfaces.ClickSocial
        };
        _track.Record(row.ShopId, surface, pageUrl: ShopCopy.Loc(row));
        return Redirect(dest);
    }
}
