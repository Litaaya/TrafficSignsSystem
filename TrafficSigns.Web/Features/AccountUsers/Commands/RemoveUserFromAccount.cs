using MediatR;
using TrafficSigns.Application.Features.AccountUsers.Commands;

namespace TrafficSigns.Web.Features.AccountUsers.Commands;


public static class RemoveUserFromAccountEndpoint
{
    public static void MapRemoveUserFromAccount(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/accounts/{accountId:guid}/users/{userId:guid}", async (Guid accountId, Guid userId, IMediator mediator) =>
        {
            try
            {
                var success = await mediator.Send(new RemoveUserFromAccountCommand(accountId, userId));
                return success ? Results.NoContent() : Results.NotFound();
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