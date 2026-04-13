using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.Users.Commands;

public record SyncUserFromKeycloakCommand(Guid UserId, string? ActorId = null) : IRequest;

public class SyncUserFromKeycloakHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService) : IRequestHandler<SyncUserFromKeycloakCommand>
{
    private async Task<string> GetActorNameAsync(string? actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return "System/Unknown";

        if (Guid.TryParse(actorId, out var guid))
        {
            var admin = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == guid);

            if (admin != null) return admin.Username;
        }

        return actorId;
    }

    public async Task Handle(SyncUserFromKeycloakCommand request, CancellationToken cancellationToken)
    {
        var kcUser = await keycloakService.GetUserByIdAsync(request.UserId);
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (kcUser == null)
        {
            if (user != null && !user.IsDeleted)
            {
                user.IsDeleted = true;
                user.UpdatedDt = DateTime.UtcNow;
                user.AddMetadataLog("sync_event", "User deactivated: Not found in Keycloak");
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var json = kcUser.Value;
        bool isNew = (user == null);

        if (isNew)
        {
            user = new User
            {
                Id = request.UserId,
                Username = json.GetProperty("username").GetString()!,
                CreatedDt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }

        if (user == null) return;

        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        var changedColumns = new List<string>();

        void TrackChange(string field, string? current, string? newVal, Action<string?> update)
        {
            if (current != newVal)
            {
                oldValues[field] = current;
                newValues[field] = newVal;
                changedColumns.Add(field);
                update(newVal);
            }
        }

        TrackChange("Username", user.Username, json.TryGetProperty("username", out var un) ? un.GetString() : null, v => user.Username = v ?? string.Empty);
        TrackChange("Email", user.Email, json.TryGetProperty("email", out var em) ? em.GetString() : null, v => user.Email = v);
        TrackChange("FirstName", user.FirstName, json.TryGetProperty("firstName", out var fn) ? fn.GetString() : null, v => user.FirstName = v);
        TrackChange("LastName", user.LastName, json.TryGetProperty("lastName", out var ln) ? ln.GetString() : null, v => user.LastName = v);

        if (json.TryGetProperty("attributes", out var attrs) && attrs.TryGetProperty("phoneNumber", out var ph))
        {
            var kcPhone = ph[0].GetString();
            TrackChange("Phone", user.Phone, kcPhone, v => user.Phone = v);
        }

        if (json.TryGetProperty("enabled", out var en))
        {
            bool shouldBeDeleted = !en.GetBoolean();
            if (user.IsDeleted != shouldBeDeleted)
            {
                oldValues["IsDeleted"] = user.IsDeleted;
                newValues["IsDeleted"] = shouldBeDeleted;
                changedColumns.Add("IsDeleted");
                user.IsDeleted = shouldBeDeleted;
            }
        }

        if (isNew || changedColumns.Any())
        {
            var now = DateTime.UtcNow;
            string actorName = await GetActorNameAsync(request.ActorId);
            string actorIdStr = request.ActorId ?? "00000000-0000-0000-0000-000000000000";
            string logSuffix = $"{actorName}({actorIdStr}) at {now:yyyy-MM-dd HH:mm:ss}";

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = "User",
                EntityId = user.Id,
                Action = isNew ? "CREATE" : "UPDATE",
                UserId = Guid.TryParse(request.ActorId, out var aid) ? aid : null,
                UserName = actorName,
                Timestamp = now,
                OldValues = isNew ? null : JsonSerializer.Serialize(oldValues),
                NewValues = JsonSerializer.Serialize(newValues),
                ChangedColumns = string.Join(", ", changedColumns)
            };
            db.AuditLogs.Add(auditLog);

            if (isNew)
            {
                user.AddMetadataLog("update_history", $"Created by {logSuffix}");
            }
            else
            {
                user.AddMetadataLog("update_history", $"Updated by {logSuffix}");
                user.AddMetadataLog("sync_diff", string.Join(" | ", changedColumns.Select(c => $"{c}: '{oldValues[c]}' -> '{newValues[c]}'")));
            }

            user.UpdatedDt = now;
            user.AddMetadataLog("sync_source", "Keycloak_Background_Poll");
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}