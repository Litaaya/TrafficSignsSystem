using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AccountUser> AccountUsers => Set<AccountUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var stringDictConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, string>?, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new Dictionary<string, string>()
        );

        var stringDictComparer = new ValueComparer<Dictionary<string, string>?>(
            (c1, c2) => JsonSerializer.Serialize(c1, jsonOptions) == JsonSerializer.Serialize(c2, jsonOptions),
            c => c == null ? 0 : JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
            c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(c, jsonOptions), jsonOptions)
        );

        var entitiesWithSimpleMetadata = new[] { typeof(Account), typeof(User), typeof(AccountUser) };
        foreach (var entityType in entitiesWithSimpleMetadata)
        {
            modelBuilder.Entity(entityType)
                .Property("Metadata")
                .HasColumnType("jsonb")
                .HasConversion(stringDictConverter)
                .Metadata.SetValueComparer(stringDictComparer);
        }

        modelBuilder.Entity<AccountUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AccountId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Account).WithMany(a => a.AccountUsers).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany(u => u.AccountUsers).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrafficSign>(entity =>
        {
            entity.HasKey(e => e.Id);

            var objectDictConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, object>, string>(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, jsonOptions) ?? new Dictionary<string, object>()
            );

            var objectDictComparer = new ValueComparer<Dictionary<string, object>>(
                (c1, c2) => JsonSerializer.Serialize(c1, jsonOptions) == JsonSerializer.Serialize(c2, jsonOptions),
                c => c == null ? 0 : JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
                c => c == null ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(c, jsonOptions), jsonOptions)!
            );

            entity.Property(e => e.Metadata)
                  .HasColumnType("jsonb")
                  .HasConversion(objectDictConverter)
                  .Metadata.SetValueComparer(objectDictComparer);

            entity.HasIndex(e => e.Location).HasMethod("gist");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.EntityId, x.Timestamp });
        });
    }
}