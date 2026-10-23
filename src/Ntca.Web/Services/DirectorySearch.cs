using Microsoft.EntityFrameworkCore;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class DirectorySearch
{
    public const int MinChars = 2;
    public const int PageSize = 10;

    public static bool CanSearch(string? q)
        => !string.IsNullOrWhiteSpace(q) && q.Trim().Length >= MinChars;

    public static string Term(string q) => q.Trim();

    public static int Page(int? page) => page is > 0 ? page.Value : 1;

    public static bool Matches(string? name, string? city, string? state, string? zip, string q)
    {
        if (!CanSearch(q))
            return false;
        var t = Term(q);
        return Contains(name, t) || Contains(city, t) || Contains(state, t) || Contains(zip, t);
    }

    public static IQueryable<Shop> FilterShops(IQueryable<Shop> src, string q)
    {
        var t = Term(q);
        return src.Where(s =>
            s.Name.Contains(t)
            || s.City.Contains(t)
            || s.StateCode.Contains(t)
            || (s.PostalCode != null && s.PostalCode.Contains(t)));
    }

    public static IQueryable<ShopEvent> FilterShows(IQueryable<ShopEvent> src, string q)
    {
        var t = Term(q);
        return src.Where(e =>
            e.Title.Contains(t)
            || (e.City != null && e.City.Contains(t))
            || (e.StateCode != null && e.StateCode.Contains(t))
            || (e.Venue != null && (
                e.Venue.Name.Contains(t)
                || e.Venue.City.Contains(t)
                || (e.Venue.PostalCode != null && e.Venue.PostalCode.Contains(t)))));
    }

    public static IQueryable<Promoter> FilterPromoters(IQueryable<Promoter> src, string q)
    {
        var t = Term(q);
        return src.Where(p => p.Name.Contains(t) || p.Slug.Contains(t));
    }

    public static IQueryable<Manufacturer> FilterManufacturers(IQueryable<Manufacturer> src, string q)
    {
        var t = Term(q);
        return src.Where(m =>
            m.Name.Contains(t)
            || m.NormalizedName.Contains(t)
            || (m.CountryName != null && m.CountryName.Contains(t)));
    }

    public static object PageResult<T>(IReadOnlyList<T> items, int page, bool hasMore)
        => new { items, page, pageSize = PageSize, hasMore };

    public static async Task<(List<T> Items, bool HasMore)> TakePageAsync<T>(
        IQueryable<T> query, int? page, CancellationToken ct)
    {
        var skip = (Page(page) - 1) * PageSize;
        var rows = await query.Skip(skip).Take(PageSize + 1).ToListAsync(ct);
        var hasMore = rows.Count > PageSize;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);
        return (rows, hasMore);
    }

    private static bool Contains(string? value, string term)
        => !string.IsNullOrEmpty(value)
           && value.Contains(term, StringComparison.OrdinalIgnoreCase);
}
