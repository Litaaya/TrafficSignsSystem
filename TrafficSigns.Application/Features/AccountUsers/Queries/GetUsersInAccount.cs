using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.AccountUsers.Queries;

public record UserInAccountDto(
    Guid UserId,
    string Username,
    string Email,
    string Phone,
    string Role,
    DateTime JoinedDt,
    Dictionary<string, string>? Metadata
);

public record GetUsersInAccountQuery(Guid AccountId) : IRequest<List<UserInAccountDto>>;

public class GetUsersInAccountHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<GetUsersInAccountQuery, List<UserInAccountDto>>
{
    public async Task<List<UserInAccountDto>> Handle(GetUsersInAccountQuery request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanGetUsersInAccountAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == request.AccountId, cancellationToken))
        {
            throw new KeyNotFoundException($"Account not found");
        }

        return await db.AccountUsers
            .AsNoTracking()
            .Where(au => au.AccountId == request.AccountId && !au.IsDeleted)
            .Include(au => au.User)
            .Select(au => new UserInAccountDto(
                au.UserId,
                au.User.Username,
                au.User.Email ?? string.Empty,
                au.User.Phone ?? string.Empty,
                au.Role,
                au.CreatedDt,
                au.Metadata
            ))
            .ToListAsync(cancellationToken);
    }
}