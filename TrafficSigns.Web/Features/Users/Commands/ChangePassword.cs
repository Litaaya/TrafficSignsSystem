using MediatR;
using TrafficSigns.Application.Features.Users.Commands;

namespace TrafficSigns.Web.Features.Users.Commands;

public static class ChangePasswordEndpoint
{
    public static void MapChangePassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/profile/change-password", async (ChangePasswordCommand command, IMediator mediator) =>
        {
            try
            {
                await mediator.Send(command);
                return Results.Ok(new { Message = "Successfully change password" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("Profile")
        .RequireAuthorization();
    }
}