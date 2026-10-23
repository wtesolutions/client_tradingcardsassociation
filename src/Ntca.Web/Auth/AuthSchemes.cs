using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Ntca.Core;

namespace Ntca.Web.Auth;

public static class AuthSchemes
{
    public const string Owner = "Ntca.Owner";
    public const string Sadmin = "Ntca.Sadmin";

    public static ClaimsPrincipal Principal(AppUser user, IEnumerable<string> roles, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new("username", user.Username)
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    public static async Task SignInAsync(HttpContext http, string scheme, AppUser user, IEnumerable<string> roles)
    {
        await http.SignInAsync(scheme, Principal(user, roles, scheme), new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
        });
    }
}
