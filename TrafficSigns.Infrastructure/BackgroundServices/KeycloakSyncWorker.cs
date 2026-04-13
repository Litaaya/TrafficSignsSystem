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
    private DateTime _lastSyncTime = DateTime.UtcNow.AddMinutes(-1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminService>();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var events = await keycloak.GetAdminEventsAsync(_lastSyncTime);

                if (events.Any())
                {
                    foreach (var ev in events)
                    {
                        if (ev.TryGetProperty("resourceType", out var rt) && rt.GetString() != "USER")
                            continue;

                        if (ev.TryGetProperty("resourcePath", out var path))
                        {
                            var userIdStr = path.GetString()?.Split('/').Last();
                            if (Guid.TryParse(userIdStr, out var userId))
                            {
                                string? actorId = null;
                                if (ev.TryGetProperty("authDetails", out var auth) && auth.TryGetProperty("userId", out var actorProp))
                                {
                                    actorId = actorProp.GetString();
                                }

                                await mediator.Send(new SyncUserFromKeycloakCommand(userId, actorId), stoppingToken);
                            }
                        }
                    }
                    _lastSyncTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Keycloak sync error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}