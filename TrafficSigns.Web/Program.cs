using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NetTopologySuite.IO.Converters;
using Scalar.AspNetCore;
using System.Text.Json;
using System.Text.Json.Serialization;

using TrafficSigns.Application;
using TrafficSigns.Application.Common.Interfaces;

using TrafficSigns.Infrastructure;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Infrastructure.Services;

using TrafficSigns.Web.Features.Accounts.Commands;
using TrafficSigns.Web.Features.Accounts.Queries;
using TrafficSigns.Web.Features.AccountUsers.Commands;
using TrafficSigns.Web.Features.AccountUsers.Queries;
using TrafficSigns.Web.Features.Map.Queries;
using TrafficSigns.Web.Features.TrafficSigns.Commands;
using TrafficSigns.Web.Features.TrafficSigns.Queries;
using TrafficSigns.Web.Features.Users.Commands;
using TrafficSigns.Web.Features.Users.Queries;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("TrafficSignsDB");
var keycloakSection = builder.Configuration.GetSection("Keycloak");
var authServerUrl = keycloakSection["AuthServerUrl"]?.TrimEnd('/');
var realmName = keycloakSection["Realm"];
var authority = $"{authServerUrl}/realms/{realmName}";

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    options.SerializerOptions.Converters.Add(new GeoJsonConverterFactory());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
        options.JsonSerializerOptions.Converters.Add(new GeoJsonConverterFactory());
    });

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var keycloakSection = builder.Configuration.GetSection("Keycloak");
        var baseUrl = keycloakSection["AuthServerUrl"]?.TrimEnd('/');
        var realm = keycloakSection["Realm"];
        var uiClientId = keycloakSection["ResourceClient:ClientId"];

        var oauthScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Log in via Keycloak to test APIs",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{baseUrl}/realms/{realm}/protocol/openid-connect/auth"),
                    TokenUrl = new Uri($"{baseUrl}/realms/{realm}/protocol/openid-connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        { "openid", "Required for OIDC" },
                        { "profile", "User info" }
                    }
                }
            }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Keycloak"] = oauthScheme;

        var schemeReference = new OpenApiSecuritySchemeReference("Keycloak", document);
        document.Security = [new OpenApiSecurityRequirement { [schemeReference] = ["openid"] }];

        return Task.CompletedTask;
    });
});

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = authority;
    options.Audience = "account";
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = authority,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var claimsIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
            if (claimsIdentity != null)
            {
                var realmAccessClaim = context.Principal?.FindFirst("realm_access")?.Value;
                if (!string.IsNullOrEmpty(realmAccessClaim))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(realmAccessClaim);
                    if (doc.RootElement.TryGetProperty("roles", out var roles))
                    {
                        foreach (var role in roles.EnumerateArray())
                        {
                            claimsIdentity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role.GetString()!));
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:7272", "http://localhost:7272")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

/*----------------------------------------------------------------------------------*/

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Traffic Signs API";

        options
            .AddPreferredSecuritySchemes("Keycloak")
            .AddAuthorizationCodeFlow("Keycloak", flow =>
            {
                flow.ClientId = builder.Configuration["Keycloak:ResourceClient:ClientId"] ?? "trafficsigns-ui";
            });
    });
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// User control
app.MapCreateUser();
app.MapDeleteUser();
app.MapReactivateUser();
app.MapUpdateUser();
app.MapUpdateProfile();

app.MapGetUsers();
app.MapValidateUserField();
app.MapResetPassword();

// Account control
app.MapCreateAccount();
app.MapDeleteAccount();
app.MapReactivateAccount();
app.MapUpdateAccount();

app.MapGetAccounts();
app.MapValidateAccountField();

// Account and User relationship control
app.MapAssignUserToAccount();
app.MapRemoveUserFromAccount();
app.MapUpdateUserInAccount();

app.MapGetUsersInAccount();
app.MapGetAccountsOfUser();

// Traffic Sign control
app.MapCreateTrafficSign();
app.MapDeleteTrafficSign();
app.MapUpdateTrafficSign();
app.MapReactivateTrafficSign();

app.MapGetTrafficSigns();
app.MapGetTrafficSignById();

// Map
app.MapRoadEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
