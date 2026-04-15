using MediatR;
using TrafficSigns.Application.Features.TrafficSigns.Commands;

namespace TrafficSigns.Web.Features.TrafficSigns.Commands;

public static class DeleteTrafficSignEndpoint
{
    public static void MapDeleteTrafficSign(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/traffic-signs/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            await mediator.Send(new DeleteTrafficSignCommand(id));
            return Results.NoContent();
        })
        .WithTags("TrafficSigns")
        .RequireAuthorization();
    }
}