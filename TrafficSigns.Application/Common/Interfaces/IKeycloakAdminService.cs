namespace TrafficSigns.Application.Common.Interfaces;

public interface IKeycloakAdminService
{
    Task<Guid> CreateUserAsync(string username, string email, string password, string firstName, string lastName);
    Task UpdateUserStatusAsync(Guid userId, bool enabled);
    Task ResetPasswordAsync(Guid userId, string newPassword);
    Task UpdateUserAsync(Guid userId, string email, string firstName, string lastName);
    Task<bool> VerifyUserPasswordAsync(string username, string password);
}