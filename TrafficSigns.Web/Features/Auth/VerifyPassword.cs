using MediatR;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.Auth;

public record VerifyPasswordRequest(string Password);

public record VerifyAdminPasswordCommand(string Password) : IRequest<bool>;

public class VerifyAdminPasswordHandler(
    IKeycloakAdminService keycloakService,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<VerifyAdminPasswordCommand, bool>
{
    public async Task<bool> Handle(VerifyAdminPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!permissionService.IsAdmin())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var username = currentUser.GetUsername();
        if (string.IsNullOrEmpty(username)) return false;

        return await keycloakService.VerifyUserPasswordAsync(username, request.Password);
    }
}

public static class VerifyPasswordEndpoint
{
    public static void MapVerifyPassword(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/verify-password", async (VerifyPasswordRequest body, IMediator mediator) =>
        {
            try
            {
                var isValid = await mediator.Send(new VerifyAdminPasswordCommand(body.Password));

                if (!isValid)
                {
                    return Results.BadRequest(new { Message = "Password verification failed." });
                }

                return Results.Ok();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in VerifyPassword: {ex.Message}");
                return Results.BadRequest(new { Message = ex.Message, StackTrace = ex.StackTrace });
            }
        })
        .WithTags("Auth")
        .RequireAuthorization();
    }
}