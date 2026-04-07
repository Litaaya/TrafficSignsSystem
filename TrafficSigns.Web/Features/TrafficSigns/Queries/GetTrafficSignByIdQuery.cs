using Marten;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Web.Features.TrafficSigns.Queries;

public record GetTrafficSignByIdQuery(Guid Id) : IRequest<TrafficSign?>;

public class GetTrafficSignByIdHandler(
    IDocumentSession session,
    IPermissionService permissionService)
    : IRequestHandler<GetTrafficSignByIdQuery, TrafficSign?>
{
    public async Task<TrafficSign?> Handle(GetTrafficSignByIdQuery request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);

        if (sign != null)
        {
            if (!await permissionService.CanAccessAccountAsync(sign.AccountId))
            {
                throw new UnauthorizedAccessException("Access denied.");
            }
        }

        return sign;
    }
}

public static class GetTrafficSignByIdEndpoint
{
    public static void MapGetTrafficSignById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/traffic-signs/{id:guid}", async (
            Guid id,
            IMediator mediator) =>
        {
            try
            {
                var query = new GetTrafficSignByIdQuery(id);
                var result = await mediator.Send(query);

                return result is not null ? Results.Ok(result) : Results.NotFound(new { Message = "Traffic sign not found." });
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