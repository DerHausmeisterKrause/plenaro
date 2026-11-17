using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Plenaro.Api.Identity;

/// <summary>
/// Partition authenticated application API limits by the actual Plenaro
/// account, not by the NGINX reverse proxy IP. Unauthenticated endpoints
/// (notably login) continue to use their own IP-based abuse protection.
/// </summary>
public static class AuthenticatedRateLimitKey
{
    public static string For(HttpContext context)
    {
        var id=context.User.Identity?.IsAuthenticated==true
            ?context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            :null;
        if(!string.IsNullOrWhiteSpace(id))
            return "user:"+id;
        return "ip:"+(context.Connection.RemoteIpAddress?.ToString()??"unknown");
    }
}
