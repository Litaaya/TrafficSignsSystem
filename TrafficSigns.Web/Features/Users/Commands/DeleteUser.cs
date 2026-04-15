using MediatR;
using TrafficSigns.Application.Features.Users.Commands;
namespace TrafficSigns.Web.Features.Users.Commands;

public static class DeleteUserEndpoint
{
    public static void MapDeleteUser(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/{userId:guid}", async (Guid userId, IMediator mediator) =>
        {
            var success = await mediator.Send(new DeleteUserCommand(userId));
            return success
                ? Results.Ok(new { Message = "User deactivated successfully" })
                : Results.NotFound();
        })
        .WithTags("Users")
        .RequireAuthorization();
    }
}