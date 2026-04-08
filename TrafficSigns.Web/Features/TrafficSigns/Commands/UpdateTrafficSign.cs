using MediatR;
using TrafficSigns.Application.Features.TrafficSigns.Commands;

namespace TrafficSigns.Web.Features.TrafficSigns.Commands;

public static class UpdateTrafficSignEndpoint
{
    public static void MapUpdateTrafficSign(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/traffic-signs/{id:guid}", async (Guid id, UpdateTrafficSignCommand command, IMediator mediator) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest(new { Message = "ID mismatch between URL and body." });
            }

            try
            {
                await mediator.Send(command);
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