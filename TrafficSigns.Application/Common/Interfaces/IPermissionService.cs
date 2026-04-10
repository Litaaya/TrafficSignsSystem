namespace TrafficSigns.Application.Common.Interfaces;

public interface IPermissionService
{
    bool IsAdmin();
    Task<string?> GetUserRoleAsync(Guid accountId);
    Task<bool> CanGetAccountsAsync();
    Task<bool> CanAccessAccountAsync(Guid accountId);
    Task<bool> CanManageAccountAsync(Guid accountId);
    Task<bool> CanUpdateAccountAsync(Guid accountId, bool updatingSystemField);
    Task<bool> CanManageAccountUsersAsync(Guid accountId);
    Task<bool> CanRemoveUserAsync(Guid accountId, Guid targetUserId);
    Task<bool> CanGetUsersInAccountAsync(Guid accountId);
    Task<bool> CanManageTrafficSignsAsync(Guid accountId);
    Task<bool> CanViewMapAsync(Guid accountId);
    Task<bool> CanManageGlobalUsersAsync();
    Task<bool> CanChangePasswordAsync(Guid targetUserId);
}