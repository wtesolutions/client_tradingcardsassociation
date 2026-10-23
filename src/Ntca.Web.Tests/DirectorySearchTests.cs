using Ntca.Core;
using Ntca.Web.Services;
using Xunit;

namespace Ntca.Web.Tests;

public sealed class DirectorySearchTests
{
    [Fact]
    public void Requires_two_characters()
    {
        Assert.False(DirectorySearch.CanSearch(""));
        Assert.False(DirectorySearch.CanSearch("B"));
        Assert.True(DirectorySearch.CanSearch("Bu"));
        Assert.Equal(10, DirectorySearch.PageSize);
    }

    [Fact]
    public void Matches_name_city_state_and_zip()
    {
        Assert.True(DirectorySearch.Matches("Burbank Sportscards", "Burbank", "CA", "91502", "bur"));
        Assert.True(DirectorySearch.Matches("Triangle Area", "Raleigh", "NC", "27601", "276"));
        Assert.True(DirectorySearch.Matches("Dave & Adam's", "Williamsville", "NY", "14221", "ny"));
        Assert.False(DirectorySearch.Matches("Burbank Sportscards", "Burbank", "CA", "91502", "raleigh"));
    }

    [Fact]
    public void Filter_shops_pages_ten_and_can_page()
    {
        var shops = Enumerable.Range(1, 15).Select(i => new Shop
        {
            ShopId = i,
            Name = $"Alpha Cards {i:00}",
            City = "Springfield",
            StateCode = "MO",
            PostalCode = "65802",
            ListingStatus = "ACTIVE"
        }).AsQueryable();
        var filtered = DirectorySearch.FilterShops(shops, "Alpha").OrderBy(s => s.Name);
        Assert.Equal(15, filtered.Count());
        var page1 = filtered.Take(DirectorySearch.PageSize).ToList();
        Assert.Equal(10, page1.Count);
        Assert.Equal("Alpha Cards 01", page1[0].Name);
    }
}
