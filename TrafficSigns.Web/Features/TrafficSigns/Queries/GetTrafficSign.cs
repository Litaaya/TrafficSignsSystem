using Marten;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Web.Features.TrafficSigns.Queries;

public record GetTrafficSignsQuery(
    Guid? AccountId = null
) : IRequest<IReadOnlyList<TrafficSign>>;

public class GetTrafficSignsHandler(
    IDocumentSession session,
    IPermissionService permissionService)
    : IRequestHandler<GetTrafficSignsQuery, IReadOnlyList<TrafficSign>>
{
    public async Task<IReadOnlyList<TrafficSign>> Handle(GetTrafficSignsQuery request, CancellationToken cancellationToken)
    {
        if (!request.AccountId.HasValue || request.AccountId == Guid.Empty)
        {
            if (!permissionService.IsAdmin())
            {
                throw new UnauthorizedAccessException("Account ID is required for non-administrators.");
            }
        }
        else
        {
            if (!await permissionService.CanAccessAccountAsync(request.AccountId.Value))
            {
                throw new UnauthorizedAccessException("Access denied.");
            }
        }

        IQueryable<TrafficSign> query = session.Query<TrafficSign>();

        if (request.AccountId.HasValue && request.AccountId != Guid.Empty)
        {
            var targetAccountId = request.AccountId.Value;
            query = query.Where(x => x.AccountId == targetAccountId);
        }

        return await query.ToListAsync(cancellationToken);
    }
}

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