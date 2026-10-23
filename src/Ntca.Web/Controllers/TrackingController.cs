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
        var row = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == shop && s.ListingStatus == "ACTIVE", ct);
        if (row is null)
            return NotFound();

        string? dest = kind.ToLowerInvariant() switch
        {
            "website" when !string.IsNullOrWhiteSpace(row.WebsiteUrl) => row.WebsiteUrl,
            "phone" when !string.IsNullOrWhiteSpace(row.Phone) => "tel:" + row.Phone,
            "directions" when ShopCopy.HasMappableAddress(row) => ShopCopy.MapsUrl(row),
            "social" => SocialUrl(row, net),
            _ => null
        };
        if (dest is null)
            return NotFound();

        var surface = kind.ToLowerInvariant() switch
        {
            "website" => ImpressionSurfaces.ClickWebsite,
            "phone" => ImpressionSurfaces.ClickPhone,
            "directions" => ImpressionSurfaces.ClickDirections,
            _ => ImpressionSurfaces.ClickSocial
        };
        _track.Record(row.ShopId, surface, pageUrl: ShopCopy.Loc(row));
        return Redirect(dest);
    }

    private static string? SocialUrl(Ntca.Core.Shop row, string? net)
    {
        if (string.Equals(net, "instagram", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(row.InstagramHandle))
            return ShopCopy.InstagramUrl(row.InstagramHandle);
        if (!string.IsNullOrWhiteSpace(row.FacebookUrl))
            return row.FacebookUrl;
        if (!string.IsNullOrWhiteSpace(row.InstagramHandle))
            return ShopCopy.InstagramUrl(row.InstagramHandle);
        return null;
    }
}
