using MediatR;
using TrafficSigns.Application.Features.Map.Queries;

namespace TrafficSigns.Web.Features.Map.Queries;

public static class MapEndpoints
{
    public static void MapRoadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/map/roads", async ([AsParameters] GetRoadsInViewQuery query, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(query);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("Map")
        .RequireAuthorization();
    }
}