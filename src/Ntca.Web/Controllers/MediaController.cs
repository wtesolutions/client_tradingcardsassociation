using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class MediaController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IFileStorage _files;

    public MediaController(NtcaDbContext db, IFileStorage files)
    {
        _db = db;
        _files = files;
    }

    [HttpGet("/media/gplace/{shopId:long}/{slot:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GooglePlacePhoto(long shopId, int slot, CancellationToken ct)
    {
        if (slot is < 1 or > 3)
            return NotFound();
        var now = DateTime.UtcNow;
        GooglePlacePhoto? photo;
        try
        {
            photo = await _db.GooglePlacePhotos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ShopId == shopId && p.Slot == slot && p.ExpiresUtc > now, ct);
        }
        catch (SqlException)
        {
            return NotFound();
        }
        if (photo is null)
            return NotFound();
        var stream = await _files.OpenReadAsync(photo.RelativePath, ct);
        if (stream is null)
            return NotFound();
        return File(stream, photo.ContentType);
    }
}
