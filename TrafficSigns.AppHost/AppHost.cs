var builder = DistributedApplication.CreateBuilder(args);

// Elasticsearch
var elasticsearch = builder.AddContainer("elasticsearch", "docker.elastic.co/elasticsearch/elasticsearch")
    .WithImageTag("8.13.0")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("xpack.security.enabled", "false")
    .WithEnvironment("ES_JAVA_OPTS", "-Xms4g -Xmx4g")
    .WithHttpEndpoint(port: 9200, targetPort: 9200, name: "http");
    //.WithBindMount("../elasticsearch_data", "/usr/share/elasticsearch/data");

// Apm server
var apmServer = builder.AddContainer("apm-server", "docker.elastic.co/apm/apm-server")
    .WithImageTag("8.13.0")
    .WithEnvironment("output.elasticsearch.hosts", "http://elasticsearch:9200")
    .WithEnvironment("apm-server.auth.anonymous.enabled", "true")
    .WithEnvironment("apm-server.kibana.enabled", "true")
    .WithEnvironment("apm-server.kibana.host", "http://kibana:5601")
    .WithHttpEndpoint(port: 8200, targetPort: 8200, name: "http")
    .WaitFor(elasticsearch);

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
    .WithBindMount("../TrafficSigns.Infrastructure/Keycloak/Realms/trafficsigns-realm.json", "/opt/keycloak/data/import/realm.json")
    .WithArgs("start-dev", "--import-realm")
    .WithUrlForEndpoint("http", _ => new()
    {
         Url = "/admin/trafficsigns-realm/console/",
         DisplayText = "Traffic-Signs Console"
    });

// API service
var apiservice = builder.AddProject<Projects.TrafficSigns_Web>("apiservice")
       .WithEnvironment("ElasticConfiguration__Uri", elasticsearch.GetEndpoint("http"))
       .WithReference(db)
       .WithReference(keycloak.GetEndpoint("http"))
       .WithEnvironment("Keycloak__AuthServerUrl", keycloak.GetEndpoint("http"))
       .WithEnvironment("Keycloak__Realm", "trafficsigns-realm")
       .WithEnvironment("Keycloak__SyncClient__ClientId", "trafficsigns-worker")
       .WithEnvironment("Keycloak__AdminClient__ClientSecret", keycloakClientSecret)
       .WithEnvironment("Keycloak__SyncClient__ClientSecret", keycloakSyncSecret)
       .WithEnvironment("ElasticApm__ServerUrl", apmServer.GetEndpoint("http"))
       .WithEnvironment("ElasticApm__ServiceName", "TrafficSigns-API")
       .WithEnvironment("ElasticApm__CentralConfig", "false")
       .WaitFor(db)
       .WaitFor(keycloak)
       .WaitFor(elasticsearch)
       .WaitFor(apmServer);

// FrontEnd
builder.AddJavaScriptApp("frontend", "../TrafficSigns.WebUI", "start")
    .WithReference(apiservice)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, name: "http", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    .WithEnvironment("KEYCLOAK_REALM", "trafficsigns-realm")
    .WithEnvironment("KEYCLOAK_CLIENT_ID", "trafficsigns-ui")
    .WithEnvironment("PORT", "4200");

// Kibana
var kibana = builder.AddContainer("kibana", "docker.elastic.co/kibana/kibana")
    .WithImageTag("8.13.0")
    .WithEnvironment("ELASTICSEARCH_HOSTS", "http://elasticsearch:9200")
    .WithHttpEndpoint(port: 5601, targetPort: 5601, name: "http")
    .WithUrlForEndpoint("http", _ => new()
    {
        Url = "/app/discover",
        DisplayText = "Kibana Analytics"
    })
    .WaitFor(elasticsearch);

builder.Build().Run();