using Microsoft.EntityFrameworkCore;
using Ntca.Core;
using Ntca.Data;

namespace Ntca.Web.Services;

public sealed class ShopSocialEditor
{
    private readonly NtcaDbContext _db;
    public ShopSocialEditor(NtcaDbContext db) => _db = db;

    public async Task<IReadOnlyList<SocialPlatform>> ActivePlatformsAsync(CancellationToken ct)
    {
        var rows = await _db.SocialPlatforms.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Label)
            .ToListAsync(ct);
        return rows.Count == 0 ? ShopSocialCatalog.BuiltIn : rows;
    }

    public async Task<IReadOnlyList<ShopSocial>> LoadAsync(long shopId, CancellationToken ct)
        => await _db.ShopSocials
            .Include(s => s.Platform)
            .Where(s => s.ShopId == shopId)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.PlatformCode)
            .ToListAsync(ct);

    public async Task<string?> UpsertAsync(long shopId, string platformCode, string raw, string source, bool verified, CancellationToken ct)
    {
        var platform = await _db.SocialPlatforms.FirstOrDefaultAsync(p => p.Code == platformCode, ct)
                       ?? ShopSocialCatalog.Find(platformCode);
        if (platform is null || !platform.IsActive)
            return "Unknown or inactive platform.";
        if (!ShopSocialNormalizer.TryNormalize(platform, raw, out var url, out var handle, out var error))
            return error;

        var now = DateTime.UtcNow;
        var row = await _db.ShopSocials.FirstOrDefaultAsync(s => s.ShopId == shopId && s.PlatformCode == platform.Code, ct);
        if (row is null)
        {
            _db.ShopSocials.Add(new ShopSocial
            {
                ShopId = shopId,
                PlatformCode = platform.Code,
                Url = url,
                Handle = handle,
                SortOrder = platform.DisplayOrder,
                Verified = verified,
                Source = source,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            row.Url = url;
            row.Handle = handle;
            row.Verified = verified;
            row.Source = source;
            row.UpdatedAt = now;
        }
        SyncLegacy(shopId, platform.Code, url, handle);
        await _db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> RemoveAsync(long shopId, long shopSocialId, CancellationToken ct)
    {
        var row = await _db.ShopSocials.FirstOrDefaultAsync(s => s.ShopSocialId == shopSocialId && s.ShopId == shopId, ct);
        if (row is null)
            return "Link not found.";
        var code = row.PlatformCode;
        _db.ShopSocials.Remove(row);
        SyncLegacy(shopId, code, null, null);
        await _db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> MoveAsync(long shopId, long shopSocialId, int delta, CancellationToken ct)
    {
        var rows = await _db.ShopSocials.Where(s => s.ShopId == shopId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.ShopSocialId).ToListAsync(ct);
        var i = rows.FindIndex(r => r.ShopSocialId == shopSocialId);
        var j = i + delta;
        if (i < 0 || j < 0 || j >= rows.Count)
            return null;
        (rows[i].SortOrder, rows[j].SortOrder) = (rows[j].SortOrder, rows[i].SortOrder);
        if (rows[i].SortOrder == rows[j].SortOrder)
        {
            rows[i].SortOrder += delta;
        }
        rows[i].UpdatedAt = DateTime.UtcNow;
        rows[j].UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return null;
    }

    private void SyncLegacy(long shopId, string code, string? url, string? handle)
    {
        var shop = _db.Shops.Local.FirstOrDefault(s => s.ShopId == shopId)
                   ?? _db.Shops.FirstOrDefault(s => s.ShopId == shopId);
        if (shop is null)
            return;
        if (code.Equals(ShopSocialCatalog.Website, StringComparison.OrdinalIgnoreCase))
            shop.WebsiteUrl = url;
        else if (code.Equals(ShopSocialCatalog.Facebook, StringComparison.OrdinalIgnoreCase))
            shop.FacebookUrl = url;
        else if (code.Equals(ShopSocialCatalog.Instagram, StringComparison.OrdinalIgnoreCase))
            shop.InstagramHandle = handle ?? url;
        shop.UpdatedAt = DateTime.UtcNow;
    }
}
