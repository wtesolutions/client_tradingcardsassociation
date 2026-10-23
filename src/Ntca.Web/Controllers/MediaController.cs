using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ntca.Data;

namespace Ntca.Web.Controllers;

public sealed class MediaController : Controller
{
    private readonly NtcaDbContext _db;
    private readonly IWebHostEnvironment _env;

    public MediaController(NtcaDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("/media/gplace/{shopId:long}/{slot:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GooglePlacePhoto(long shopId, int slot, CancellationToken ct)
    {
        if (slot is < 1 or > 3)
            return NotFound();
        var now = DateTime.UtcNow;
        Ntca.Core.GooglePlacePhoto? photo;
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
        var root = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        var full = Path.GetFullPath(Path.Combine(root, photo.RelativePath.Replace('\\', '/')));
        if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(full))
            return NotFound();
        return PhysicalFile(full, photo.ContentType);
    }
}
