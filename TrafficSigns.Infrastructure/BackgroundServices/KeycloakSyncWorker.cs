using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Features.Users.Commands;

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

                var events = await keycloak.GetAdminEventsAsync(DateTime.UtcNow);

                if (events.Any())
                {
                    var syncRegistry = new Dictionary<Guid, string?>();
                    long maxTimeInBatch = _lastSyncTimeMs;

                    foreach (var ev in events)
                    {
                        long eventTime = ev.GetProperty("time").GetInt64();
                        if (eventTime <= _lastSyncTimeMs)
                        {
                            continue; 
                        }

                        if (eventTime > maxTimeInBatch)
                        {
                            maxTimeInBatch = eventTime;
                        }

                        if (ev.TryGetProperty("resourceType", out var rt) && rt.GetString() == "USER")
                        {
                            var userIdStr = ev.GetProperty("resourcePath").GetString()?.Split('/').Last();
                            if (Guid.TryParse(userIdStr, out var userId))
                            {
                                string? actorId = ev.TryGetProperty("authDetails", out var auth)
                                    ? auth.GetProperty("userId").GetString() : null;

                                syncRegistry[userId] = actorId;
                            }
                        }
                    }

                    if (syncRegistry.Any())
                    {
                        foreach (var item in syncRegistry)
                        {
                            await mediator.Send(new SyncUserFromKeycloakCommand(item.Key, item.Value), stoppingToken);
                        }
                    }

                    _lastSyncTimeMs = maxTimeInBatch;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Keycloak sync failed. RelationalId: {RelationalId}. Message: {Message}",
                System.Diagnostics.Activity.Current?.TraceId,
                ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}