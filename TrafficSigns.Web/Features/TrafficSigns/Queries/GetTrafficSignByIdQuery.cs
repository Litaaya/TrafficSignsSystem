using MediatR;
using TrafficSigns.Application.Features.TrafficSigns.Queries;

namespace TrafficSigns.Web.Features.TrafficSigns.Queries;

public static class GetTrafficSignByIdEndpoint
{
    public static void MapGetTrafficSignById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/traffic-signs/{id:guid}", async (
            Guid id,
            IMediator mediator) =>
        {
            var query = new GetTrafficSignByIdQuery(id);
            var result = await mediator.Send(query);

            return result is not null ? Results.Ok(result) : Results.NotFound(new { Message = "Traffic sign not found." });
        })
        .WithTags("TrafficSigns")
        .RequireAuthorization();
    }
}