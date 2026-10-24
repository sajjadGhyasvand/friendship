using System.Security.Claims;
using FriendShip.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FriendShip.Web.Services;

public sealed class AccountCookieEvents(
    AuthService authService) : CookieAuthenticationEvents
{
    public const string SecurityStampClaim = "friendship:security_stamp";

    public override async Task ValidatePrincipal(
        CookieValidatePrincipalContext context)
    {
        var idText = context.Principal?
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var stamp = context.Principal?
            .FindFirst(SecurityStampClaim)?.Value;

        if (Guid.TryParse(idText, out var userId) &&
            userId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(stamp) &&
            await authService.IsSessionValidAsync(userId, stamp))
        {
            return;
        }

        context.RejectPrincipal();

        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
