using TrafficSigns.Application.Features.TrafficSigns.Commands;
using MediatR;

namespace TrafficSigns.Web.Features.TrafficSigns.Commands;

public static class CreateTrafficSignEndpoint
{
    public static void MapCreateTrafficSign(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/traffic-signs", async (CreateTrafficSignCommand command, IMediator mediator) =>
        {
            var signId = await mediator.Send(command);
            return Results.Created($"/api/traffic-signs/{signId}", new { Id = signId });
        })
        .WithTags("TrafficSigns")
        .RequireAuthorization();
    }
}