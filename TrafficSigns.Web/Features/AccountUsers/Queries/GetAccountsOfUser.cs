using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.AccountUsers.Queries;

public record AccountOfUserDto(
    Guid AccountId,
    string AccountName,
    bool IsOwner,
    bool IsSystem,
    DateTime JoinedDt
);

public record GetAccountsOfUserQuery(Guid UserId) : IRequest<List<AccountOfUserDto>>;

public class GetAccountsOfUserHandler(
    AppDbContext db,
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
                .Where(a => !a.Inactive)
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
            .Where(au => au.UserId == request.UserId && !au.Inactive)
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

public static class GetAccountsOfUserEndpoint
{
    public static void MapGetAccountsOfUser(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/{userId:guid}/accounts", async (Guid userId, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new GetAccountsOfUserQuery(userId));
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
        .WithTags("AccountUsers")
        .RequireAuthorization();
    }
}