using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.AccountUsers.Queries;

public record UserInAccountDto(
    Guid UserId,
    string Username,
    string Email,
    bool IsOwner,
    DateTime JoinedDt,
    Dictionary<string, string>? Metadata
);

public record GetUsersInAccountQuery(Guid AccountId) : IRequest<List<UserInAccountDto>>;

public class GetUsersInAccountHandler(
    AppDbContext db,
    IPermissionService permissionService) : IRequestHandler<GetUsersInAccountQuery, List<UserInAccountDto>>
{
    public async Task<List<UserInAccountDto>> Handle(GetUsersInAccountQuery request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanGetUsersInAccountAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        return await db.AccountUsers
            .AsNoTracking()
            .Where(au => au.AccountId == request.AccountId && !au.Inactive)
            .Include(au => au.User)
            .Select(au => new UserInAccountDto(
                au.UserId,
                au.User.Username,
                au.User.Email ?? string.Empty,
                au.Role == "Owner",
                au.CreatedDt,
                au.Metadata
            ))
            .ToListAsync(cancellationToken);
    }
}

public static class GetUsersInAccountEndpoint
{
    public static void MapGetUsersInAccount(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts/{accountId:guid}/users", async (Guid accountId, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new GetUsersInAccountQuery(accountId));
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