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

            await mediator.Send(command);
            return Results.NoContent();
        })
        .WithTags("Users")
        .RequireAuthorization();
    }
}