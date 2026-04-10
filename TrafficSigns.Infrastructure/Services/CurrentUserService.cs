using System.Security.Claims;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace TrafficSigns.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private static readonly Guid SystemBotId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string SystemBotName = "KEYCLOAK_SYNC_BOT";

    public string? GetUsername()
    {
        var name = httpContextAccessor.HttpContext?.User?.Identity?.Name
                   ?? httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;

        return name ?? SystemBotName;
    }

    public Guid? GetUserId()
    {
        var context = httpContextAccessor.HttpContext;

        if (context == null) return SystemBotId;

        var headerId = context.Request.Headers["X-Actor-Id"].ToString();
        if (!string.IsNullOrEmpty(headerId) && Guid.TryParse(headerId, out var actorGuid))
        {
            return actorGuid;
        }

        var idClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? context.User?.FindFirst("sub")?.Value
                   ?? context.User?.FindFirst("id")?.Value;

        return idClaim != null && Guid.TryParse(idClaim, out var guid) ? guid : SystemBotId;
    }

    public bool IsInRole(string role)
    {
        var context = httpContextAccessor.HttpContext;

        if (context == null) return true;

        return context.User?.IsInRole(role) ?? false;
    }
}