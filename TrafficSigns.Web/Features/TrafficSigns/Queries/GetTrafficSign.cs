using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrafficSigns.Application.Features.TrafficSigns.Queries;

namespace TrafficSigns.Web.Features.TrafficSigns.Queries;

public static class GetTrafficSignsEndpoint
{
    public static void MapGetTrafficSigns(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/traffic-signs", async (
            [FromQuery] Guid? accountId,
            IMediator mediator
            ) =>
        {
            try
            {
                var query = new GetTrafficSignsQuery(accountId);
                var result = await mediator.Send(query);

                return Results.Ok(result);
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