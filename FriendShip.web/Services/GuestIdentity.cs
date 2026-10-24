using System.Security.Cryptography;
using FriendShip.Application.Common;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;

namespace FriendShip.Web.Services;

public sealed class GuestIdentity
{
    private const string CookieName = "__Host-FriendShip.Guest";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly IHttpContextAccessor _accessor;
    private readonly ITimeLimitedDataProtector _protector;

    private Guid? _cachedVisitorId;

    public GuestIdentity(
        IHttpContextAccessor accessor,
        IDataProtectionProvider protectionProvider)
    {
        _accessor = accessor;
        _protector = protectionProvider
            .CreateProtector("FriendShip.Web.GuestIdentity.v1")
            .ToTimeLimitedDataProtector();
    }

    private HttpContext Context =>
        _accessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP request.");

    public bool TryGetCaller(out Caller caller)
    {
        caller = default!;

        if (_cachedVisitorId is Guid cachedId)
        {
            caller = CreateCaller(cachedId);
            return true;
        }

        if (!Context.Request.Cookies.TryGetValue(
                CookieName, out var protectedValue))
        {
            return false;
        }

        try
        {
            var value = _protector.Unprotect(
                protectedValue,
                out DateTimeOffset _);

            if (!Guid.TryParseExact(value, "N", out var visitorId) ||
                visitorId == Guid.Empty)
            {
                return false;
            }

            _cachedVisitorId = visitorId;
            caller = CreateCaller(visitorId);
            return true;
        }
        catch (CryptographicException)
        {
            // Invalid, expired, or protected with an unavailable key.
            return false;
        }
    }

    public Caller EnsureCaller()
    {
        if (TryGetCaller(out var existing))
            return existing;

        if (!Context.Request.IsHttps)
        {
            throw new InvalidOperationException(
                "Guest identity requires HTTPS.");
        }

        var visitorId = Guid.NewGuid();

        var protectedValue = _protector.Protect(
            visitorId.ToString("N"),
            Lifetime);

        Context.Response.Cookies.Append(
            CookieName,
            protectedValue,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = Lifetime,
                IsEssential = true
            });

        _cachedVisitorId = visitorId;

        return CreateCaller(visitorId);
    }

    private Caller CreateCaller(Guid visitorId)
    {
        Guid? userId = null;

        if (Context.User.Identity?.IsAuthenticated == true)
        {
            var id = Context.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(id, out var parsed) &&
                parsed != Guid.Empty)
            {
                userId = parsed;
            }
        }

        return new Caller(
            UserId: userId,
            VisitorId: visitorId,
            Ip: Context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            IsAdmin: false);
    }

}
