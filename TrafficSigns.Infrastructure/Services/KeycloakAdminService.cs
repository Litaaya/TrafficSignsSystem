using System.Net.Http.Headers;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace TrafficSigns.Infrastructure.Services;

public class KeycloakAdminService(HttpClient httpClient, IConfiguration config, ICurrentUserService currentUserService) : IKeycloakAdminService
{
    private async Task<string> GetAdminTokenAsync()
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];

        var isBot = currentUserService.GetUsername() == "KEYCLOAK_SYNC_BOT";

        var clientId = isBot
        ? (config["Keycloak:SyncClient:ClientId"] ?? "traffic-signs-worker")
        : (config["Keycloak:AdminClient:ClientId"] ?? config["Keycloak:ClientId"]);

        var clientSecret = isBot
            ? config["Keycloak:SyncClient:ClientSecret"]
            : (config["Keycloak:AdminClient:ClientSecret"] ?? config["Keycloak:ClientSecret"]);

        var response = await httpClient.PostAsync(
            $"{baseUrl}/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!
            }));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to obtain Keycloak token: {error}");
        }

        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        return data.GetProperty("access_token").GetString()!;
    }

    public async Task<Guid> CreateUserAsync(string username, string email, string password, string firstName, string lastName)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = new
        {
            username = username.Trim(),
            email = email,
            enabled = true,
            firstName = firstName,
            lastName = lastName,
            emailVerified = true,
            credentials = new[] { new { type = "password", value = password, temporary = true } }
        };

        var response = await httpClient.PostAsJsonAsync($"{baseUrl}/admin/realms/{realm}/users", payload);
        if (!response.IsSuccessStatusCode) throw new Exception("Failed to create Keycloak user");

        var getResponse = await httpClient.GetAsync($"{baseUrl}/admin/realms/{realm}/users?username={username.Trim()}");
        var users = await getResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        return Guid.Parse(users![0].GetProperty("id").GetString()!);
    }

    public async Task UpdateUserStatusAsync(Guid userId, bool enabled)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = new { enabled = enabled };
        await httpClient.PutAsJsonAsync($"{baseUrl}/admin/realms/{realm}/users/{userId}", payload);
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = new
        {
            type = "password",
            value = newPassword,
            temporary = false
        };

        var response = await httpClient.PutAsJsonAsync(
            $"{baseUrl}/admin/realms/{realm}/users/{userId}/reset-password",
            payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error when create password on Keycloak: {error}");
        }
    }

    public async Task<bool> VerifyUserPasswordAsync(string username, string password)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var clientId = config["Keycloak:AdminClient:ClientId"] ?? config["Keycloak:ClientId"];
        var clientSecret = config["Keycloak:AdminClient:ClientSecret"] ?? config["Keycloak:ClientSecret"];

        var response = await httpClient.PostAsync(
            $"{baseUrl}/realms/{realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = clientId!,
                ["client_secret"] = clientSecret!,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid"
            }));

        return response.IsSuccessStatusCode;
    }

    public async Task UpdateUserAsync(Guid userId, string email, string firstName, string lastName)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userUpdateDto = new
        {
            email = email,
            firstName = firstName,
            lastName = lastName,
            emailVerified = true
        };

        var response = await httpClient.PutAsJsonAsync($"{baseUrl}/admin/realms/{realm}/users/{userId}", userUpdateDto);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to update Keycloak user: {error}");
        }
    }

    public async Task<List<JsonElement>> GetAdminEventsAsync(DateTime? dateFrom)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var dateFromStr = dateFrom?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        var url = $"{baseUrl}/admin/realms/{realm}/admin-events?dateFrom={dateFromStr}&resourceType=USER";

        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return new List<JsonElement>();

        return await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? new List<JsonElement>();
    }

    public async Task<JsonElement?> GetUserByIdAsync(Guid userId)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"];
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync($"{baseUrl}/admin/realms/{realm}/users/{userId}");

        if (!response.IsSuccessStatusCode) 
        {
            var errorDetails = await response.Content.ReadAsStringAsync();
            throw new Exception($"Keycloak Error ({response.StatusCode}): {errorDetails}");
        };

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}