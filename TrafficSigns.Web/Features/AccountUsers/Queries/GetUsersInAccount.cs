using MediatR;
using TrafficSigns.Application.Features.AccountUsers.Queries;

namespace TrafficSigns.Web.Features.AccountUsers.Queries;

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