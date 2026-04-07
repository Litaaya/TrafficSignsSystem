using Microsoft.EntityFrameworkCore;
using TrafficSigns.Domain.Models;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AccountUser> AccountUsers => Set<AccountUser>();
    public DbSet<OsmRoadSegment> OsmRoadSegments => Set<OsmRoadSegment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var stringDictConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, string>?, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new Dictionary<string, string>()
        );

        modelBuilder.Entity<Account>()
            .Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(stringDictConverter);

        modelBuilder.Entity<User>()
            .Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(stringDictConverter);

        modelBuilder.Entity<AccountUser>()
            .Property(e => e.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(stringDictConverter);

        modelBuilder.Entity<TrafficSign>(entity =>
        {
            entity.HasKey(e => e.Id);

            var objectDictConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, object>, string>(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, jsonOptions) ?? new Dictionary<string, object>()
            );

            entity.Property(e => e.Metadata)
                  .HasColumnType("jsonb")
                  .HasConversion(objectDictConverter);

            entity.HasIndex(e => e.Location)
                  .HasMethod("gist");

            entity.HasIndex(e => e.RoadSegmentId);
        });

        modelBuilder.Entity<AccountUser>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Account)
                  .WithMany(a => a.AccountUsers)
                  .HasForeignKey(e => e.AccountId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.AccountUsers)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OsmRoadSegment>(entity =>
        {
            entity.ToTable("traffic_signs_map", t => t.ExcludeFromMigrations());
            entity.HasKey(e => e.SegmentId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.EntityId, x.Timestamp });
        });
    }
}