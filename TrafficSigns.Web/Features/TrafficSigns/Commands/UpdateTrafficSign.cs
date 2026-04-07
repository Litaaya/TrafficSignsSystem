using Marten;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.Persistence;

namespace TrafficSigns.Web.Features.TrafficSigns.Commands;

public record UpdateTrafficSignCommand(
    Guid Id,
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    long RoadSegmentId,
    bool IsForwardDirection,
    Dictionary<string, object>? Metadata = null
) : IRequest<bool>;

public class UpdateTrafficSignHandler(
    IDocumentSession session,
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateTrafficSignCommand, bool>
{
    public async Task<bool> Handle(UpdateTrafficSignCommand request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);
        if (sign == null)
        {
            throw new Exception($"Traffic sign with ID {request.Id} was not found.");
        }

        if (!await permissionService.CanManageTrafficSignsAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (sign.Inactive)
        {
            throw new Exception("Traffic sign is inactivated and cannot be updated.");
        }

        var roadQuery = db.Database.SqlQueryRaw<int>("SELECT 1 FROM traffic_signs_map WHERE segment_id = {0} LIMIT 1", request.RoadSegmentId);
        var roadExists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(roadQuery, cancellationToken);

        if (!roadExists)
        {
            throw new Exception($"Road Segment ID {request.RoadSegmentId} is invalid.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var mergedMetadata = sign.Metadata != null
            ? new Dictionary<string, object>(sign.Metadata)
            : new Dictionary<string, object>();

        string newLogEntry = $"Updated by {actor}({actorId}) at {timestamp}";

        if (mergedMetadata.TryGetValue("update_history", out var oldHistoryObj) && oldHistoryObj is string oldHistory)
        {
            mergedMetadata["update_history"] = $"{oldHistory}\n{newLogEntry}";
        }
        else
        {
            mergedMetadata["update_history"] = newLogEntry;
        }

        if (request.Metadata != null)
        {
            foreach (var kvp in request.Metadata)
            {
                if (kvp.Key != "update_history")
                {
                    mergedMetadata[kvp.Key] = kvp.Value;
                }
            }
        }

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        var @event = new TrafficSignUpdated(
            request.Id,
            request.Code.Trim().ToUpper(),
            request.Name.Trim(),
            location,
            request.RoadSegmentId,
            request.IsForwardDirection,
            mergedMetadata
        );

        session.SetHeader("user-id", actorId?.ToString() ?? Guid.Empty.ToString());
        session.SetHeader("user-name", actor);

        session.Events.Append(request.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public static class UpdateTrafficSignEndpoint
{
    public static void MapUpdateTrafficSign(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/traffic-signs/{id:guid}", async (Guid id, UpdateTrafficSignCommand command, IMediator mediator) =>
        {
            if (id != command.Id)
            {
                return Results.BadRequest(new { Message = "ID mismatch between URL and body." });
            }

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
        .WithTags("TrafficSigns")
        .RequireAuthorization();
    }
}