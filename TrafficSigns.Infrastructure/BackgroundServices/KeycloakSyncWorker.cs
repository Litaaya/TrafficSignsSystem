using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Features.Users.Commands;

namespace TrafficSigns.Infrastructure.BackgroundServices;

public class KeycloakSyncWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private DateTime _lastSyncTime = DateTime.UtcNow.AddMinutes(-5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Keycloak Sync Worker is starting ...");

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
                        var resourceType = ev.GetProperty("resourceType").GetString();
                        if (resourceType != "USER") continue;

                        var operationType = ev.GetProperty("operationType").GetString();
                        var resourcePath = ev.GetProperty("resourcePath").GetString();
                        var userIdStr = resourcePath?.Split('/').Last();

                        if (Guid.TryParse(userIdStr, out var userId))
                        {
                            await mediator.Send(new SyncUserFromKeycloakCommand(userId), stoppingToken);
                        }
                    }

                    _lastSyncTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync Error] {DateTime.Now}: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}