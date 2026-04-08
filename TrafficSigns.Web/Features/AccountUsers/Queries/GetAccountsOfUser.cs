using MediatR;
using TrafficSigns.Application.Features.AccountUsers.Queries;

namespace TrafficSigns.Web.Features.AccountUsers.Queries;

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