using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.Map.Queries;

public record RoadDto(long SegmentId, string? Name, int HighwayId, int OnewayType, Geometry Geometry);

public record GetRoadsInViewQuery(
    double MinLat,
    double MinLng,
    double MaxLat,
    double MaxLng,
    int Zoom = 15
) : IRequest<List<RoadDto>>;

public class GetRoadsInViewHandler(IApplicationDbContext db) : IRequestHandler<GetRoadsInViewQuery, List<RoadDto>>
{
    private readonly GeometryFactory _factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public async Task<List<RoadDto>> Handle(GetRoadsInViewQuery request, CancellationToken cancellationToken)
    {
        var bbox = _factory.CreatePolygon(new Coordinate[] {
            new(request.MinLng, request.MinLat),
            new(request.MaxLng, request.MinLat),
            new(request.MaxLng, request.MaxLat),
            new(request.MinLng, request.MaxLat),
            new(request.MinLng, request.MinLat)
        });

        var query = db.OsmRoadSegments.AsNoTracking().Where(r => r.Way.Intersects(bbox));

        if (request.Zoom < 14)
        {
            query = query.Where(r => r.HighwayId <= 9);
        }

        return await query
            .OrderByDescending(r => r.ZOrder)
            .Select(r => new RoadDto(r.SegmentId, r.Name, r.HighwayId, r.OnewayType, r.Way))
            .Take(1500)
            .ToListAsync(cancellationToken);
    }
}
