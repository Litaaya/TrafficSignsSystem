using MediatR;
using TrafficSigns.Application.Features.Users.Commands;

namespace TrafficSigns.Web.Features.Users.Commands;

public static class ResetPasswordEndpoint
{
    public static void MapResetPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users/{id:guid}/reset-password", async (Guid id, ResetPasswordByAdminCommand command, IMediator mediator) =>
        {
            if (id != command.Id)
                return Results.BadRequest(new { Message = "Id user invalid" });

            try
            {
                await mediator.Send(command);
                return Results.NoContent();
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