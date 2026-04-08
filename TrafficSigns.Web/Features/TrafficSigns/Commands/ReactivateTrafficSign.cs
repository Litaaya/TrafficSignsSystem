using MediatR;
using TrafficSigns.Application.Features.TrafficSigns.Commands;

namespace TrafficSigns.Web.Features.TrafficSigns.Commands;

public static class ReactivateTrafficSignEndpoint
{
    public static void MapReactivateTrafficSign(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/traffic-signs/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new ReactivateTrafficSignCommand(id));
                return Results.Ok(new { Message = "Traffic Sign reactivated successfully" });
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