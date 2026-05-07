using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Infrastructure.Persistence;

namespace TrafficSigns.Infrastructure.Services;

public class PermissionService(AppDbContext db, ICurrentUserService currentUserService) : IPermissionService
{
    private string? _cachedRole;
    private Guid? _cachedAccountId;

    private static class Roles
    {
        public const string Admin = "admin";
        public const string Owner = "Owner";
        public const string Member = "Member";
    }

    public bool IsAdmin() => currentUserService.IsInRole(Roles.Admin);

    public async Task<string?> GetUserRoleAsync(Guid accountId)
    {
        if (_cachedRole != null && _cachedAccountId == accountId)
        {
            return _cachedRole;
        }

        var userId = currentUserService.GetUserId();
        if (userId == null) return null;

        _cachedRole = await db.AccountUsers
            .Where(au => au.AccountId == accountId && au.UserId == userId && !au.IsDeleted)
            .Select(au => au.Role)
            .FirstOrDefaultAsync();

        _cachedAccountId = accountId;
        return _cachedRole;
    }

    private async Task<bool> HasAccessAsync(Guid accountId, params string[] allowedRoles)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        if (role == null) return false;

        return allowedRoles.Length == 0 || allowedRoles.Contains(role);
    }

    public async Task<bool> CanGetAccountsAsync() => true;

    public async Task<bool> CanAccessAccountAsync(Guid accountId)
        => await HasAccessAsync(accountId);

    public async Task<bool> CanManageAccountAsync(Guid accountId)
        => await HasAccessAsync(accountId, Roles.Owner);

    public async Task<bool> CanUpdateAccountAsync(Guid accountId, bool updatingSystemField)
    {
        if (IsAdmin()) return true;
        if (updatingSystemField) return false;

        return await HasAccessAsync(accountId, Roles.Owner);
    }

    public async Task<bool> CanManageAccountUsersAsync(Guid accountId)
        => await HasAccessAsync(accountId, Roles.Owner);

    public async Task<bool> CanRemoveUserAsync(Guid accountId, Guid targetUserId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        var currentUserId = currentUserService.GetUserId();

        return role == Roles.Owner || currentUserId == targetUserId;
    }

    public async Task<bool> CanGetUsersInAccountAsync(Guid accountId)
        => await HasAccessAsync(accountId, Roles.Owner);

    public async Task<bool> CanManageTrafficSignsAsync(Guid accountId)
        => await HasAccessAsync(accountId, Roles.Owner, Roles.Member);

    public async Task<bool> CanViewMapAsync(Guid accountId)
        => await HasAccessAsync(accountId);

    public async Task<bool> CanManageGlobalUsersAsync() => IsAdmin();
}