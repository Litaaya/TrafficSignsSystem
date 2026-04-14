using JasperFx;
using Marten;
using Marten.Events.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.IO.Converters;
using Npgsql;
using System.Text.Json.Serialization;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using TrafficSigns.Infrastructure.BackgroundServices;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Infrastructure.Persistence.Interceptors;
using Weasel.Core;

namespace TrafficSigns.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TrafficSignsDB")
            ?? throw new InvalidOperationException("Connection string 'TrafficSignsDB' not found.");

        services.AddNpgsqlDataSource(connectionString, builder =>
        {
            builder.UseNetTopologySuite();
            builder.EnableDynamicJson();
        });

        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();

            options.UseNpgsql(dataSource, o => o.UseNetTopologySuite())
                   .AddInterceptors(auditInterceptor);
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.AddMarten(opts =>
        {
            opts.AutoCreateSchemaObjects = AutoCreate.All;
            opts.DatabaseSchemaName = "public";
            opts.Projections.Snapshot<TrafficSign>(SnapshotLifecycle.Inline);
            opts.Events.MetadataConfig.HeadersEnabled = true;
            opts.Listeners.Add(new MartenTraceInterceptor());

            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase, options =>
            {
                options.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
                options.Converters.Add(new GeoJsonConverterFactory());
            });
        })
        .UseLightweightSessions()
        .UseNpgsqlDataSource();

        services.AddHostedService<KeycloakSyncWorker>();

        return services;
    }
}