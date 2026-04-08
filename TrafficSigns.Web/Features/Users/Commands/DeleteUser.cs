using MediatR;
using TrafficSigns.Application.Features.Users.Commands;
namespace TrafficSigns.Web.Features.Users.Commands;

public static class DeleteUserEndpoint
{
    public static void MapDeleteUser(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/{userId:guid}", async (Guid userId, IMediator mediator) =>
        {
            try
            {
                var success = await mediator.Send(new DeleteUserCommand(userId));
                return success
                    ? Results.Ok(new { Message = "User deactivated successfully" })
                    : Results.NotFound();
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
        .WithTags("Users")
        .RequireAuthorization();
    }
}