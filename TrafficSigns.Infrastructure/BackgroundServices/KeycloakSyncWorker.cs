using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Features.Users.Commands;
using System.Diagnostics;

namespace TrafficSigns.Infrastructure.BackgroundServices;

public class KeycloakSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<KeycloakSyncWorker> logger) : BackgroundService
{
    private long _lastSyncTimeMs = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminService>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var adminEvents = await keycloak.GetAdminEventsAsync(DateTime.UtcNow);
                var userEvents = await keycloak.GetUserEventsAsync(DateTime.UtcNow);

                var syncRegistry = new Dictionary<Guid, (string? ActorId, string? ActionType)>();
                long maxTimeInBatch = _lastSyncTimeMs;

                foreach (var ev in adminEvents)
                {
                    long eventTime = ev.GetProperty("time").GetInt64();
                    if (eventTime <= _lastSyncTimeMs) continue;
                    if (eventTime > maxTimeInBatch) maxTimeInBatch = eventTime;

                    if (ev.TryGetProperty("resourceType", out var rt) && rt.GetString() == "USER")
                    {
                        var userIdStr = ev.GetProperty("resourcePath").GetString()?.Split('/').Last();
                        if (Guid.TryParse(userIdStr, out var userId))
                        {
                            string? actorId = ev.TryGetProperty("authDetails", out var auth)
                                ? auth.GetProperty("userId").GetString() : null;

                            string? operation = ev.TryGetProperty("operationType", out var op) ? op.GetString() : "ADMIN_SYNC";

                            syncRegistry[userId] = (actorId, operation);
                        }
                    }
                }

                foreach (var ev in userEvents)
                {
                    long eventTime = ev.GetProperty("time").GetInt64();
                    if (eventTime <= _lastSyncTimeMs) continue;
                    if (eventTime > maxTimeInBatch) maxTimeInBatch = eventTime;

                    string? type = ev.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type != null && type.StartsWith("UPDATE_"))
                    {
                        var userIdStr = ev.GetProperty("userId").GetString();
                        if (Guid.TryParse(userIdStr, out var userId))
                        {
                            syncRegistry[userId] = (userIdStr, type);
                        }
                    }
                }

                if (syncRegistry.Any())
                {
                    foreach (var item in syncRegistry)
                    {
                        using var activity = new Activity("SyncUserFromKeycloak").Start();

                        try
                        {
                            var userId = item.Key;
                            var (actorId, actionType) = item.Value;

                            await mediator.Send(new SyncUserFromKeycloakCommand(userId, actorId, actionType), stoppingToken);

                            logger.LogInformation("Sync [{Action}] triggered for User: {UserId} by Actor: {ActorId}. TraceId: {TraceId}",
                                actionType, userId, actorId, activity.TraceId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Individual user sync failed. UserID: {UserId}", item.Key);
                        }
                    }
                }

                _lastSyncTimeMs = maxTimeInBatch;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Keycloak sync batch failed. Message: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}