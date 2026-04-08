using Marten;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.TrafficSigns.Queries;

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
