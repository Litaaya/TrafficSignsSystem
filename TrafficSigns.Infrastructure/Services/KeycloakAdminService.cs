using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Infrastructure.Services;

public class KeycloakAdminService(
    HttpClient httpClient,
    IConfiguration config,
    ICurrentUserService currentUserService,
    IMemoryCache memoryCache
    ) : IKeycloakAdminService
{
    private async Task<string> GetAdminTokenAsync()
    {
        var isBot = currentUserService.GetUsername() == "KEYCLOAK_SYNC_BOT";
        var cacheKey = isBot ? "KeycloakToken_Worker" : "KeycloakToken_Admin";

        if (memoryCache.TryGetValue(cacheKey, out string? cachedToken))
        {
            return cachedToken!;
        }

        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];

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
            throw new HttpRequestException($"Keycloak Authentication failed with status {response.StatusCode}: {error}");
        }

        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = data.GetProperty("access_token").GetString()!;
        
        var expiresIn = data.GetProperty("expires_in").GetInt32();

        memoryCache.Set(cacheKey, token, TimeSpan.FromSeconds(expiresIn - 30));
        return token;
    }

    public async Task<Guid> CreateUserAsync(string username, string email, string phone, string password, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required for user creation.");

        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        var payload = new
        {
            username = username.Trim(),
            email,
            enabled = true,
            firstName,
            lastName,
            emailVerified = true,
            credentials = new[] { new { type = "password", value = password, temporary = true } },
            attributes = new Dictionary<string, string[]> { { "phone", [phone] } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/admin/realms/{realm}/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Failed to create Keycloak user. Status: {response.StatusCode}. Error: {error}");
        }

        var location = response.Headers.Location;
        var userIdStr = location?.PathAndQuery.Split('/').Last();

        if (string.IsNullOrEmpty(userIdStr))
        {
            throw new InvalidOperationException("Keycloak created the user but failed to return a location header containing the User ID.");
        }

        return Guid.Parse(userIdStr);
    }

    public async Task UpdateUserStatusAsync(Guid userId, bool enabled)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/admin/realms/{realm}/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { enabled });

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to update status for user {userId}. Status: {response.StatusCode}");
        }
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword, bool isTemporary)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        var payload = new { type = "password", value = newPassword, temporary = isTemporary };

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/admin/realms/{realm}/users/{userId}/reset-password");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to reset password for user {userId}. Status: {response.StatusCode}");
        }
    }

    public async Task UpdateUserAsync(Guid userId, string email, string phone, string firstName, string lastName)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        var payload = new
        {
            email,
            firstName,
            lastName,
            emailVerified = true,
            attributes = new Dictionary<string, string[]> { { "phone", [phone] } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/admin/realms/{realm}/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to update user {userId}. Status: {response.StatusCode}");
        }
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{baseUrl}/admin/realms/{realm}/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to delete user {userId}. Status: {response.StatusCode}");
        }
    }

    public async Task<List<JsonElement>> GetUsersAsync(int first, int max)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/admin/realms/{realm}/users?first={first}&max={max}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to retrieve users from Keycloak. Status: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
    }

    public async Task<List<string>> GetUserRolesAsync(Guid userId)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/admin/realms/{realm}/users/{userId}/role-mappings/realm");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to retrieve roles for user {userId}. Status: {response.StatusCode}");
        }

        var roles = await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
        return roles.Select(r => r.GetProperty("name").GetString()!).ToList();
    }

    public async Task<List<JsonElement>> GetAdminEventsAsync(DateTime? dateFrom)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        var dateFromStr = dateFrom?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var url = $"{baseUrl}/admin/realms/{realm}/admin-events?dateFrom={dateFromStr}&resourceType=USER";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch admin events. Status: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
    }

    public async Task<List<JsonElement>> GetUserEventsAsync(DateTime? dateFrom)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        var dateFromStr = dateFrom?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var url = $"{baseUrl}/admin/realms/{realm}/events?dateFrom={dateFromStr}&type=UPDATE_PROFILE";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch user events. Status: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<List<JsonElement>>() ?? [];
    }

    public async Task<JsonElement?> GetUserByIdAsync(Guid userId)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
        var realm = config["Keycloak:Realm"];
        var accessToken = await GetAdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/admin/realms/{realm}/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch user by ID {userId}. Status: {response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<bool> VerifyUserPasswordAsync(string username, string password)
    {
        var baseUrl = config["Keycloak:AuthServerUrl"]?.TrimEnd('/');
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
}