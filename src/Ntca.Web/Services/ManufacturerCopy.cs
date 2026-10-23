using Ntca.Core;

namespace Ntca.Web.Services;

public static class ManufacturerCopy
{
    public static string? LogoSrc(Manufacturer? mfr)
    {
        var url = mfr?.LogoUrl?.Trim();
        return string.IsNullOrEmpty(url) ? null : url;
    }

    public static bool HasDescription(Manufacturer? mfr)
        => !string.IsNullOrWhiteSpace(mfr?.DescriptionHtml);
}
