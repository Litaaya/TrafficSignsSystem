using Marten;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.TrafficSigns.Queries;

public record GetTrafficSignByIdQuery(Guid Id) : IRequest<TrafficSign>;

public class GetTrafficSignByIdHandler(
    IDocumentSession session,
    IPermissionService permissionService)
    : IRequestHandler<GetTrafficSignByIdQuery, TrafficSign?>
{
    public async Task<TrafficSign?> Handle(GetTrafficSignByIdQuery request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);

        if (sign == null)
            throw new KeyNotFoundException($"Traffic sign with ID {request.Id} not found");

        if (!await permissionService.CanAccessAccountAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        return sign;
    }
}