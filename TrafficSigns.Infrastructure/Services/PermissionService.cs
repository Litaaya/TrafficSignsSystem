using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Infrastructure.Persistence;

namespace TrafficSigns.Infrastructure.Services;

public class PermissionService(AppDbContext db, ICurrentUserService currentUserService) : IPermissionService
{
    public bool IsAdmin() => currentUserService.IsInRole("admin");

    public async Task<string?> GetUserRoleAsync(Guid accountId)
    {
        var userId = currentUserService.GetUserId();
        if (userId == null) return null;

        return await db.AccountUsers
            .Where(au => au.AccountId == accountId && au.UserId == userId && !au.Inactive)
            .Select(au => au.Role)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CanGetAccountsAsync()
    {
        return true;
    }

    public async Task<bool> CanAccessAccountAsync(Guid accountId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        return role != null;
    }

    public async Task<bool> CanManageAccountAsync(Guid accountId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        return role == "Owner";
    }

    public async Task<bool> CanUpdateAccountAsync(Guid accountId, bool updatingSystemField)
    {
        if (IsAdmin()) return true;

        if (updatingSystemField) return false;
        if (updatingSystemField) return false;

        var role = await GetUserRoleAsync(accountId);
        return role == "Owner";
    }

    public async Task<bool> CanManageAccountUsersAsync(Guid accountId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        return role == "Owner";
    }

    public async Task<bool> CanRemoveUserAsync(Guid accountId, Guid targetUserId)
    {
        if (IsAdmin()) return true;

        var currentUserRole = await GetUserRoleAsync(accountId);
        var currentUserId = currentUserService.GetUserId();

        return currentUserRole == "Owner" || currentUserId == targetUserId;
    }

    public async Task<bool> CanGetUsersInAccountAsync(Guid accountId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        return role == "Owner";
    }

    public async Task<bool> CanManageTrafficSignsAsync(Guid accountId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        return role == "Owner" || role == "Member";
    }

    public async Task<bool> CanViewMapAsync(Guid accountId)
    {
        if (IsAdmin()) return true;

        var role = await GetUserRoleAsync(accountId);
        return role != null;
    }

    public async Task<bool> CanManageGlobalUsersAsync()
    {
        return IsAdmin();
    }
}