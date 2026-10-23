using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace Ntca.Web.Services;

public sealed class ShopMediaService
{
    public const int MaxPhotos = 12;
    public const long MaxBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/jpg", "image/png", "image/webp" };

    private readonly NtcaDbContext _db;
    private readonly IFileStorage _files;
    private readonly ImpersonationService _imp;

    public ShopMediaService(NtcaDbContext db, IFileStorage files, ImpersonationService imp)
    {
        _db = db;
        _files = files;
        _imp = imp;
    }

    public async Task<List<ShopPhoto>> ListAsync(long shopId, CancellationToken ct)
        => await _db.ShopPhotos.AsNoTracking()
            .Where(p => p.ShopId == shopId && p.ModerationStatus == "APPROVED")
            .OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).ThenBy(p => p.PhotoId)
            .ToListAsync(ct);

    public async Task<Dictionary<long, ShopPublicMedia>> LoadPublicAsync(IEnumerable<Shop> shops, CancellationToken ct)
    {
        var list = shops.ToList();
        var ids = list.Select(s => s.ShopId).Distinct().ToList();
        var photos = ids.Count == 0
            ? []
            : await _db.ShopPhotos.AsNoTracking()
                .Where(p => ids.Contains(p.ShopId) && p.ModerationStatus == "APPROVED")
                .OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).ThenBy(p => p.PhotoId)
                .ToListAsync(ct);
        var byShop = photos.GroupBy(p => p.ShopId).ToDictionary(g => g.Key, g => g.ToList());
        var map = new Dictionary<long, ShopPublicMedia>();
        foreach (var shop in list)
        {
            var rows = byShop.GetValueOrDefault(shop.ShopId) ?? [];
            var views = rows.Select(p => new ShopPhotoView
            {
                PhotoId = p.PhotoId,
                ThumbUrl = ThumbUrl(_files, p),
                CardUrl = CardUrl(_files, p),
                LargeUrl = LargeUrl(_files, p),
                Alt = p.AltText,
                Caption = p.Caption,
                IsCover = p.IsCover
            }).ToList();
            var cover = views.FirstOrDefault(v => v.IsCover) ?? views.FirstOrDefault();
            map[shop.ShopId] = new ShopPublicMedia
            {
                LogoUrl = LogoUrl(_files, shop),
                CoverCardUrl = cover?.CardUrl,
                CoverLargeUrl = cover?.LargeUrl,
                Photos = views
            };
        }
        return map;
    }

    public async Task UndoFyiAsync(ShopEditRequest row, CancellationToken ct)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(row.ProposedJson) ? "{}" : row.ProposedJson);
        var r = doc.RootElement;
        var kind = r.TryGetProperty("kind", out var k) ? k.GetString() : "";
        var shop = await _db.Shops.FirstOrDefaultAsync(s => s.ShopId == row.ShopId, ct);
        if (shop is null)
            return;
        if (kind == "logo")
        {
            var prev = r.TryGetProperty("previous", out var p) ? p.GetString() : null;
            shop.LogoObjectKey = string.IsNullOrWhiteSpace(prev) ? null : prev;
        }
        else if (kind == "photo"
                 && r.TryGetProperty("action", out var a)
                 && a.GetString() == "upload"
                 && r.TryGetProperty("photoId", out var idEl)
                 && idEl.TryGetInt64(out var photoId))
        {
            var photo = await _db.ShopPhotos.FirstOrDefaultAsync(p => p.PhotoId == photoId && p.ShopId == shop.ShopId, ct);
            if (photo is not null)
                photo.ModerationStatus = "REMOVED";
        }
        row.Status = "UNDONE";
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> SaveLogoAsync(Shop shop, long userId, IFormFile file, CancellationToken ct)
    {
        var err = Validate(file);
        if (err is not null)
            return err;
        await using var input = file.OpenReadStream();
        var webp = await EncodeAsync(input, 400, 400, square: true, ct);
        var key = $"shops/{shop.ShopId}/logo.webp";
        await using var ms = new MemoryStream(webp);
        var url = await _files.SaveAsync(key, ms, "image/webp", ct);
        var previous = shop.LogoObjectKey;
        shop.LogoObjectKey = key;
        ShopAdminCopy.AddLock(shop, "photos");
        shop.UpdatedAt = DateTime.UtcNow;
        await NoteFyiAsync(shop.ShopId, userId, $"{{\"kind\":\"logo\",\"previous\":\"{previous}\",\"current\":\"{key}\"}}", ct);
        await _db.SaveChangesAsync(ct);
        await AuditChange(shop.ShopId, userId, "OWNER_LOGO", $"{{\"key\":\"{key}\"}}", ct);
        _ = url;
        return null;
    }

    public async Task<string?> SavePhotoAsync(Shop shop, long userId, IFormFile file, string? alt, string? caption, CancellationToken ct)
    {
        var count = await _db.ShopPhotos.CountAsync(p => p.ShopId == shop.ShopId && p.ModerationStatus == "APPROVED", ct);
        if (count >= MaxPhotos)
            return "This shop already has the maximum of 12 photos.";
        var err = Validate(file);
        if (err is not null)
            return err;
        await using var input = file.OpenReadStream();
        using var buf = new MemoryStream();
        await input.CopyToAsync(buf, ct);
        var bytes = buf.ToArray();
        var hash = SHA256.HashData(bytes);
        if (await _db.ShopPhotos.AnyAsync(p => p.ShopId == shop.ShopId && p.ContentHash == hash && p.ModerationStatus == "APPROVED", ct))
            return "That photo is already on this listing.";

        var photo = new ShopPhoto
        {
            PublicId = Guid.NewGuid(),
            ShopId = shop.ShopId,
            UploadedByUserId = userId,
            PhotoKind = "INTERIOR",
            ContentHash = hash,
            Caption = Blank(caption),
            AltText = Blank(alt),
            SortOrder = count,
            IsCover = count == 0,
            ModerationStatus = "APPROVED",
            CreatedAt = DateTime.UtcNow,
            StorageUrl = ""
        };
        _db.ShopPhotos.Add(photo);
        await _db.SaveChangesAsync(ct);

        var prefix = $"shops/{shop.ShopId}/photos/{photo.PhotoId}";
        await using var orig = new MemoryStream(bytes);
        var large = await EncodeAsync(orig, 1600, 1600, square: false, ct);
        orig.Position = 0;
        var card = await EncodeAsync(orig, 640, 640, square: false, ct);
        orig.Position = 0;
        var thumb = await EncodeAsync(orig, 160, 160, square: false, ct);
        photo.StorageUrl = await SaveKey(prefix + "-large.webp", large, ct);
        photo.ThumbnailUrl = await SaveKey(prefix + "-thumb.webp", thumb, ct);
        await SaveKey(prefix + "-card.webp", card, ct);
        photo.ObjectKey = prefix;
        ShopAdminCopy.AddLock(shop, "photos");
        shop.UpdatedAt = DateTime.UtcNow;
        await NoteFyiAsync(shop.ShopId, userId, $"{{\"kind\":\"photo\",\"action\":\"upload\",\"photoId\":{photo.PhotoId}}}", ct);
        await _db.SaveChangesAsync(ct);
        await AuditChange(shop.ShopId, userId, "OWNER_PHOTO", $"{{\"photoId\":{photo.PhotoId}}}", ct);
        return null;
    }

    public async Task DeletePhotoAsync(Shop shop, long photoId, long userId, CancellationToken ct)
    {
        var photo = await _db.ShopPhotos.FirstOrDefaultAsync(p => p.PhotoId == photoId && p.ShopId == shop.ShopId, ct);
        if (photo is null)
            return;
        photo.ModerationStatus = "REMOVED";
        await NoteFyiAsync(shop.ShopId, userId, $"{{\"kind\":\"photo\",\"action\":\"delete\",\"photoId\":{photoId}}}", ct);
        await _db.SaveChangesAsync(ct);
        await AuditChange(shop.ShopId, userId, "OWNER_PHOTO_DEL", $"{{\"photoId\":{photoId}}}", ct);
    }

    public async Task SetCoverAsync(Shop shop, long photoId, long userId, CancellationToken ct)
    {
        var photos = await _db.ShopPhotos.Where(p => p.ShopId == shop.ShopId && p.ModerationStatus == "APPROVED").ToListAsync(ct);
        foreach (var p in photos)
            p.IsCover = p.PhotoId == photoId;
        shop.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditChange(shop.ShopId, userId, "OWNER_COVER", $"{{\"photoId\":{photoId}}}", ct);
    }

    public async Task ReorderAsync(Shop shop, long photoId, int delta, long userId, CancellationToken ct)
    {
        var photos = await _db.ShopPhotos
            .Where(p => p.ShopId == shop.ShopId && p.ModerationStatus == "APPROVED")
            .OrderBy(p => p.SortOrder).ThenBy(p => p.PhotoId)
            .ToListAsync(ct);
        var i = photos.FindIndex(p => p.PhotoId == photoId);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= photos.Count)
            return;
        (photos[i].SortOrder, photos[j].SortOrder) = (photos[j].SortOrder, photos[i].SortOrder);
        await _db.SaveChangesAsync(ct);
        await AuditChange(shop.ShopId, userId, "OWNER_REORDER", $"{{\"photoId\":{photoId},\"delta\":{delta}}}", ct);
    }

    public static string? CardUrl(IFileStorage files, ShopPhoto photo)
        => string.IsNullOrWhiteSpace(photo.ObjectKey) ? photo.StorageUrl : files.PublicUrl(photo.ObjectKey + "-card.webp");

    public static string? LargeUrl(IFileStorage files, ShopPhoto photo)
        => string.IsNullOrWhiteSpace(photo.ObjectKey) ? photo.StorageUrl : files.PublicUrl(photo.ObjectKey + "-large.webp");

    public static string? ThumbUrl(IFileStorage files, ShopPhoto photo)
        => string.IsNullOrWhiteSpace(photo.ThumbnailUrl) && !string.IsNullOrWhiteSpace(photo.ObjectKey)
            ? files.PublicUrl(photo.ObjectKey + "-thumb.webp")
            : photo.ThumbnailUrl;

    public static string? LogoUrl(IFileStorage files, Shop shop)
        => string.IsNullOrWhiteSpace(shop.LogoObjectKey) ? null : files.PublicUrl(shop.LogoObjectKey);

    private async Task<string> SaveKey(string key, byte[] bytes, CancellationToken ct)
    {
        await using var ms = new MemoryStream(bytes);
        return await _files.SaveAsync(key, ms, "image/webp", ct);
    }

    private static string? Validate(IFormFile file)
    {
        if (file.Length <= 0)
            return "Choose a photo.";
        if (file.Length > MaxBytes)
            return "Photos must be 10 MB or smaller.";
        var type = file.ContentType ?? "";
        var ext = Path.GetExtension(file.FileName);
        if (!Types.Contains(type) && ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return "Use a JPG, PNG, or WebP image.";
        return null;
    }

    private static async Task<byte[]> EncodeAsync(Stream input, int maxW, int maxH, bool square, CancellationToken ct)
    {
        using var image = await Image.LoadAsync(input, ct);
        if (image.Metadata.ExifProfile is not null)
        {
            image.Metadata.ExifProfile.RemoveValue(ExifTag.GPSLatitude);
            image.Metadata.ExifProfile.RemoveValue(ExifTag.GPSLongitude);
            image.Metadata.ExifProfile.RemoveValue(ExifTag.GPSAltitude);
            image.Metadata.ExifProfile.RemoveValue(ExifTag.GPSLatitudeRef);
            image.Metadata.ExifProfile.RemoveValue(ExifTag.GPSLongitudeRef);
        }
        image.Mutate(x =>
        {
            x.AutoOrient();
            if (square)
            {
                var side = Math.Min(image.Width, image.Height);
                x.Crop(new Rectangle((image.Width - side) / 2, (image.Height - side) / 2, side, side));
                x.Resize(maxW, maxH);
            }
            else
            {
                x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxW, maxH) });
            }
        });
        using var outMs = new MemoryStream();
        await image.SaveAsWebpAsync(outMs, new WebpEncoder { Quality = 82 }, ct);
        return outMs.ToArray();
    }

    private async Task NoteFyiAsync(long shopId, long userId, string json, CancellationToken ct)
    {
        _db.ShopEditRequests.Add(new ShopEditRequest
        {
            PublicId = Guid.NewGuid(),
            ShopId = shopId,
            RequestedByUserId = userId,
            ProposedJson = json,
            RequestKind = "FYI",
            Status = "FYI",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await Task.CompletedTask;
    }

    private async Task AuditChange(long shopId, long userId, string action, string json, CancellationToken ct)
    {
        var state = _imp.Read();
        var payload = $"{{\"action\":\"{action}\",\"attribution\":\"{_imp.Attribution(state, "owner")}\",\"detail\":{json}}}";
        await _imp.AuditAsync(action, "Shop", shopId, state.StaffUserId, userId, payload, ct);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public sealed class ShopPublicMedia
{
    public string? LogoUrl { get; init; }
    public string? CoverCardUrl { get; init; }
    public string? CoverLargeUrl { get; init; }
    public IReadOnlyList<ShopPhotoView> Photos { get; init; } = [];
}

public sealed class ShopPhotoView
{
    public long PhotoId { get; init; }
    public string? ThumbUrl { get; init; }
    public string? CardUrl { get; init; }
    public string? LargeUrl { get; init; }
    public string? Alt { get; init; }
    public string? Caption { get; init; }
    public bool IsCover { get; init; }
}
