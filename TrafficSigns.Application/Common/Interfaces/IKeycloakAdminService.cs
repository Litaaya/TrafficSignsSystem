using System.Text.Json;

namespace TrafficSigns.Application.Common.Interfaces;

public interface IKeycloakAdminService
{
    Task<Guid> CreateUserAsync(string username, string email, string password, string firstName, string lastName, string phone); 
    Task UpdateUserAsync(Guid userId, string email, string firstName, string lastName, string phone);
    Task UpdateUserStatusAsync(Guid userId, bool enabled);
    Task DeleteUserAsync(Guid userId);
    Task ResetPasswordAsync(Guid userId, string newPassword);

    Task<JsonElement?> GetUserByIdAsync(Guid userId);
    Task<List<JsonElement>> GetUsersAsync(int first = 0, int max = 100);
    Task<List<JsonElement>> GetAdminEventsAsync(DateTime? dateFrom);

    Task<bool> VerifyUserPasswordAsync(string username, string password);
    Task<List<string>> GetUserRolesAsync(Guid userId);
}