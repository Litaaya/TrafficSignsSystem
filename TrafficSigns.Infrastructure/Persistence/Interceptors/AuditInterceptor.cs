using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Infrastructure.Persistence.Interceptors;

public class AuditInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var context = eventData.Context;
        if (context != null)
        {
            var auditEntries = OnBeforeSaveChanges(context);
            if (auditEntries.Count > 0) context.Set<AuditLog>().AddRange(auditEntries);
        }
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = OnBeforeSaveChanges(context);

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLog>().AddRange(auditEntries);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditLog> OnBeforeSaveChanges(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditLog>();

        var userIdRaw = currentUser.GetUserId();
        Guid? userId = null;
        if (userIdRaw != null)
        {
            if (userIdRaw is Guid g) userId = g;
            else if (Guid.TryParse(userIdRaw.ToString(), out var parsedGuid)) userId = parsedGuid;
        }

        var userName = currentUser.GetUsername();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = GetEntityId(entry),
                Action = entry.State.ToString().ToUpper(),
                UserId = userId,
                UserName = userName,
                Timestamp = DateTime.UtcNow 
            };

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();
            var changedColumns = new List<string>();

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;

                if (propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.IsModified)
                    {
                        changedColumns.Add(propertyName);
                        newValues[propertyName] = "[PROTECTED_CHANGE]";
                    }

                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        newValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        oldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            var original = property.OriginalValue;
                            var current = property.CurrentValue;

                            if (!Equals(original, current))
                            {
                                oldValues[propertyName] = original;
                                newValues[propertyName] = current;
                                changedColumns.Add(propertyName);
                            }
                        }
                        break;
                }
            }

            if (entry.State == EntityState.Modified && changedColumns.Count == 0)
                continue;

            auditLog.OldValues = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues);
            auditLog.NewValues = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues);
            auditLog.ChangedColumns = changedColumns.Count == 0 ? null : string.Join(", ", changedColumns);

            auditEntries.Add(auditLog);
        }

        return auditEntries;
    }

    private Guid GetEntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey == null) return Guid.Empty;

        var idName = primaryKey.Properties.Select(p => p.Name).FirstOrDefault();
        if (idName != null && entry.Property(idName).CurrentValue is Guid guidValue)
        {
            return guidValue;
        }

        return Guid.Empty;
    }
}