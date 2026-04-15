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
            IMediator mediator) =>
        {
            var query = new GetTrafficSignsQuery(accountId);
            var result = await mediator.Send(query);

            return Results.Ok(result);
        })
        .WithTags("TrafficSigns")
        .RequireAuthorization();
    }
}