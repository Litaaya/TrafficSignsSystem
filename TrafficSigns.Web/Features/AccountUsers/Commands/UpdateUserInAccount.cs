using MediatR;
using TrafficSigns.Application.Features.AccountUsers.Commands;

namespace TrafficSigns.Web.Features.AccountUsers.Commands;

public static class UpdateUserInAccountEndpoint
{
    public static void MapUpdateUserInAccount(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/accounts/{accountId:guid}/users/{userId:guid}/role", async (
            Guid accountId,
            Guid userId,
            UpdateUserInAccountRequest request,
            IMediator mediator) =>
        {
            try
            {
                var command = new UpdateUserInAccountCommand(
                    accountId,
                    userId,
                    request.Role);

                var success = await mediator.Send(command);
                return Results.Ok(new { Message = "User role updated successfully" });
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