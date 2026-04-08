using MediatR;
using TrafficSigns.Application.Features.TrafficSigns.Commands;

namespace TrafficSigns.Web.Features.TrafficSigns.Commands;

public static class DeleteTrafficSignEndpoint
{
    public static void MapDeleteTrafficSign(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/traffic-signs/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new DeleteTrafficSignCommand(id));
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("TrafficSigns")
        .RequireAuthorization();
    }
}