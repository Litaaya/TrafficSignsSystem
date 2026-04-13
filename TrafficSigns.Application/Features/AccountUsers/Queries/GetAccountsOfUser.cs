using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.AccountUsers.Queries;
public record AccountOfUserDto(
    Guid AccountId,
    string AccountName,
    bool IsOwner,
    bool IsSystem,
    DateTime JoinedDt
);

public record GetAccountsOfUserQuery(Guid UserId) : IRequest<List<AccountOfUserDto>>;

public class GetAccountsOfUserHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUserService,
    IPermissionService permissionService) : IRequestHandler<GetAccountsOfUserQuery, List<AccountOfUserDto>>
{
    public async Task<List<AccountOfUserDto>> Handle(GetAccountsOfUserQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();

        if (!permissionService.IsAdmin() && currentUserId != request.UserId)
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (permissionService.IsAdmin() && currentUserId == request.UserId)
        {
            return await db.Accounts
                .AsNoTracking()
                .Where(a => !a.IsDeleted)
                .Select(a => new AccountOfUserDto(
                    a.Id,
                    a.Name,
                    true,
                    a.System,
                    a.CreatedDt
                ))
                .ToListAsync(cancellationToken);
        }

        return await db.AccountUsers
            .AsNoTracking()
            .Where(au => au.UserId == request.UserId && !au.IsDeleted)
            .Include(au => au.Account)
            .Select(au => new AccountOfUserDto(
                au.AccountId,
                au.Account.Name,
                au.Role == "Owner",
                au.Account.System,
                au.CreatedDt
            ))
            .ToListAsync(cancellationToken);
    }
}
