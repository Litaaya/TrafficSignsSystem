using MediatR;
using TrafficSigns.Application.Features.Users.Commands;

namespace TrafficSigns.Web.Features.Users.Commands;

public static class ReactivateUserEndpoint
{
    public static void MapReactivateUser(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/users/{id:guid}/reactivate", async (Guid id, ReactivateUserRequest body, IMediator mediator) =>
        {
            try
            {
                var command = new ReactivateUserCommand(id, body.NewPassword);
                var userId = await mediator.Send(command);

                return Results.Ok(new
                {
                    Message = "User reactivated successfully",
                    Id = userId
                });
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