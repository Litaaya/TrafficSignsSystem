using System.Security.Claims;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace TrafficSigns.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? GetUsername() =>
        httpContextAccessor.HttpContext?.User?.Identity?.Name
        ?? httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value;

    public Guid? GetUserId()
    {
        var context = httpContextAccessor.HttpContext;
        if (context == null) return null;

        var headerId = context.Request.Headers["X-Actor-Id"].ToString();
        if (!string.IsNullOrEmpty(headerId) && Guid.TryParse(headerId, out var actorGuid))
        {
            return actorGuid;
        }

        var idClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? context.User?.FindFirst("sub")?.Value
                   ?? context.User?.FindFirst("id")?.Value;

        return idClaim != null && Guid.TryParse(idClaim, out var guid) ? guid : null;
    }

    public bool IsInRole(string role)
    {
        return httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }
}