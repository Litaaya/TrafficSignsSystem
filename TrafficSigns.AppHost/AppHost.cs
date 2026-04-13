var builder = DistributedApplication.CreateBuilder(args);

// Security
var keycloakClientSecret = builder.AddParameter("keycloak-client-secret", secret: true);
var keycloakSyncSecret = builder.AddParameter("keycloak-sync-secret", secret: true);
var postgresPassword = builder.AddParameter("postgres-password", "postgres", secret: true);

// Database
var postgres = builder.AddPostgres("postgres")
                      .WithImage("postgis/postgis")
                      .WithImageTag("17-3.5")
                      .WithPassword(postgresPassword)
                      .WithDataVolume()
                      .WithHostPort(9999);
var db = postgres.AddDatabase("TrafficSignsDB");
var keycloakDb = postgres.AddDatabase("keycloakdb");

// Keycloak
var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak", "latest")
    .WithReference(keycloakDb)
    .WithHttpEndpoint(port: 8181, targetPort: 8080, name: "http")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL", "jdbc:postgresql://postgres:5432/keycloakdb")
    .WithEnvironment("KC_DB_USERNAME", "postgres")
    .WithEnvironment("KC_DB_PASSWORD", "postgres")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithBindMount("./TrafficSigns.Infrastructure/Keycloak/Realms/trafficsigns-realm.json", "/opt/keycloak/data/import/realm.json")
    .WithArgs("start-dev", "--import-realm")
    .WithUrlForEndpoint("http", _ => new()
     {
         Url = "/admin/trafficsigns-realm/console/",
         DisplayText = "Traffic-Signs Console"
     });    

// API service
var apiservice = builder.AddProject<Projects.TrafficSigns_Web>("apiservice")
       .WithReference(db)
       .WithReference(keycloak.GetEndpoint("http"))
       .WithEnvironment("Keycloak__AuthServerUrl", keycloak.GetEndpoint("http"))
       .WithEnvironment("Keycloak__Realm", "trafficsigns-realm")
       .WithEnvironment("Keycloak__SyncClient__ClientId", "trafficsigns-worker")
       .WithEnvironment("Keycloak__AdminClient__ClientSecret", keycloakClientSecret)
       .WithEnvironment("Keycloak__SyncClient__ClientSecret", keycloakSyncSecret)
       .WaitFor(db)
       .WaitFor(keycloak);

// FrontEnd
builder.AddJavaScriptApp("frontend", "../TrafficSigns.WebUI", "start")
    .WithReference(apiservice)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, name: "http", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    .WithEnvironment("KEYCLOAK_REALM", "trafficsigns-realm")
    .WithEnvironment("KEYCLOAK_CLIENT_ID", "trafficsigns-ui")
    .WithEnvironment("PORT", "4200");

builder.Build().Run();