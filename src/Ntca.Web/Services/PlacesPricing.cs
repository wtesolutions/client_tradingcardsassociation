namespace Ntca.Web.Services;

/// <summary>
/// Published Google Maps Platform list prices (USD per 1,000 events, first paid
/// tier after the monthly free cap). Source: developers.google.com/maps/billing-and-pricing/pricing
/// last checked 2026-10-23. Highest SKU in the field mask wins.
/// </summary>
public static class PlacesPricing
{
    public const decimal TextSearchProPerThousand = 32.00m;
    public const decimal TextSearchEnterprisePerThousand = 35.00m;
    public const decimal TextSearchAtmospherePerThousand = 40.00m;
    public const decimal PlaceDetailsProPerThousand = 17.00m;
    public const decimal PlaceDetailsEnterprisePerThousand = 20.00m;
    public const decimal PlaceDetailsAtmospherePerThousand = 25.00m;
    public const decimal PlacePhotoPerThousand = 7.00m;

    public const int TextSearchProFree = 5_000;
    public const int TextSearchEnterpriseFree = 1_000;
    public const int PlaceDetailsAtmosphereFree = 1_000;
    public const int PlacePhotoFree = 1_000;

    public const string MatchSku = "Places API Text Search Enterprise";
    public const string DetailsSku = "Places API Place Details Enterprise + Atmosphere";
    public const string PhotoSku = "Places API Place Details Photos";

    public static decimal AfterCap(int calls, int freeCap, decimal perThousand)
        => Math.Max(0, calls - freeCap) / 1000m * perThousand;

    public static decimal ListPrice(int calls, decimal perThousand)
        => calls / 1000m * perThousand;

    public static string FormatUsd(decimal amount)
        => amount.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
