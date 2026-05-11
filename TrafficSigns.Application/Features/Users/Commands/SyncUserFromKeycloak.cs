using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.Users.Commands;

public record SyncUserFromKeycloakCommand(
    Guid UserId,
    string? ActorId = null,
    string? ActionType = "KEYCLOAK_SYNC") : IRequest;

public class SyncUserFromKeycloakHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService,
    IAuditActorProvider actorProvider) : IRequestHandler<SyncUserFromKeycloakCommand>
{
    public async Task Handle(SyncUserFromKeycloakCommand request, CancellationToken cancellationToken)
    {
        var kcUser = await keycloakService.GetUserByIdAsync(request.UserId);
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (!string.IsNullOrEmpty(request.ActorId) && Guid.TryParse(request.ActorId, out var actorId))
        {
            var actorUser = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == actorId, cancellationToken);

            actorProvider.ActorId = actorId;

            actorProvider.ActorName = actorUser?.Username ?? "Keycloak User";
            actorProvider.OverrideAction = request.ActionType;

            if (actorUser != null) actorUser.LastActiveDt = DateTime.UtcNow;
        }

        if (kcUser == null)
        {
            if (user != null && !user.IsDeleted)
            {
                user.IsDeleted = true;
                user.UpdatedDt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var json = kcUser.Value;
        bool isNew = (user == null);

        if (isNew)
        {
            user = new User { Id = request.UserId, CreatedDt = DateTime.UtcNow };
            db.Users.Add(user);
        }

        user!.Username = json.TryGetProperty("username", out var un) ? (un.GetString() ?? string.Empty) : user.Username;
        user.Email = json.TryGetProperty("email", out var em) ? em.GetString() : user.Email;
        user.FirstName = json.TryGetProperty("firstName", out var fn) ? fn.GetString() : user.FirstName;
        user.LastName = json.TryGetProperty("lastName", out var ln) ? ln.GetString() : user.LastName;

        if (json.TryGetProperty("enabled", out var en))
            user.IsDeleted = !en.GetBoolean();

        if (json.TryGetProperty("attributes", out var attrs) &&
            attrs.TryGetProperty("phone", out var ph) && ph.GetArrayLength() > 0)
        {
            user.Phone = ph[0].GetString();
        }

        user.UpdatedDt = DateTime.UtcNow;

        var traceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();

        var manualLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "KeycloakIdentity",
            EntityId = request.UserId,
            Action = request.ActionType ?? "SYNC",
            UserId = actorProvider.ActorId,
            UserName = actorProvider.ActorName,
            Timestamp = DateTime.UtcNow,
            RelationalId = traceId,
            NewValues = JsonSerializer.Serialize(new { Message = "Identity synchronized from Keycloak event" })
        };

        db.AuditLogs.Add(manualLog);

        // 4. LƯU TẤT CẢ
        await db.SaveChangesAsync(cancellationToken);
    }
}