using System.Text;
using Ntca.Core;

namespace Ntca.Web.Services;

public static class ShopMarkdown
{
    public static string Render(Shop shop, string summary, IReadOnlyList<ShopHours>? hours = null, IReadOnlyList<ShopEvent>? events = null, string? tierName = null)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(shop.Name).AppendLine();
        sb.AppendLine();
        sb.Append(shop.City).Append(", ").Append(ShopCopy.StateName(shop.StateCode)).AppendLine();
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("## Details");
        if (!string.IsNullOrWhiteSpace(shop.AddressLine1))
            sb.Append("- Address: ").Append(shop.AddressLine1).Append(", ").Append(shop.City).Append(", ").Append(shop.StateCode).Append(' ').Append(shop.PostalCode).AppendLine();
        if (!string.IsNullOrWhiteSpace(shop.Phone))
            sb.Append("- Phone: ").Append(shop.Phone).AppendLine();
        if (!string.IsNullOrWhiteSpace(shop.PublicEmail))
            sb.Append("- Email: ").Append(shop.PublicEmail).AppendLine();
        if (!string.IsNullOrWhiteSpace(shop.WebsiteUrl))
            sb.Append("- Website: ").Append(shop.WebsiteUrl).AppendLine();
        var spec = ShopCopy.SpecialtySentence(shop.PrimarySpecialty);
        if (spec != null)
            sb.Append("- Specialty: ").Append(spec.TrimEnd('.')).AppendLine();
        var hour = ShopCopy.HoursSentence(hours);
        if (hour != null)
            sb.Append("- ").Append(hour).AppendLine();
        if (!string.IsNullOrWhiteSpace(tierName))
            sb.Append("- NTCA listing: ").Append(tierName).Append(" · ").Append(shop.ClaimStatus).AppendLine();
        if (events is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Upcoming events");
            foreach (var ev in events)
                sb.Append("- ").Append(ev.StartsAt.ToString("yyyy-MM-dd")).Append(" — ").Append(ev.Title).AppendLine();
        }
        sb.AppendLine();
        sb.Append("Canonical page: ").Append(ShopCopy.Loc(shop)).AppendLine();
        return sb.ToString();
    }

    public static string Directory(IReadOnlyList<Shop> shops, string origin)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# NTCA shop directory");
        sb.AppendLine();
        sb.AppendLine("Plain-text shop listings for LLM crawlers. Each shop also has a `.md` page.");
        sb.AppendLine();
        foreach (var shop in shops)
        {
            sb.Append("- [").Append(shop.Name).Append("](")
                .Append(origin.TrimEnd('/')).Append(ShopCopy.MdLoc(shop)).Append(") — ")
                .Append(shop.City).Append(", ").Append(shop.StateCode).AppendLine();
        }
        return sb.ToString();
    }
}
