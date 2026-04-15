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
            var command = new UpdateUserInAccountCommand(
                accountId,
                userId,
                request.Role);

            var success = await mediator.Send(command);
            return Results.Ok(new { Message = "User role updated successfully" });
        })
        .WithTags("AccountUsers")
        .RequireAuthorization();
    }
}