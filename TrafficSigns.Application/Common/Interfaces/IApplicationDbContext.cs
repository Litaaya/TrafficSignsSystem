using Microsoft.EntityFrameworkCore;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Account> Accounts { get; }
    DbSet<User> Users { get; }
    DbSet<AccountUser> AccountUsers { get; }
    DbSet<OsmRoadSegment> OsmRoadSegments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}