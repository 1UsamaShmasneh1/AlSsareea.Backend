using AlSsareea.Modules.Media.Application;
using AlSsareea.Modules.Media.Contracts;
using AlSsareea.Modules.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AlSsareea.Modules.Media.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString("MediaDatabase");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:MediaDatabase is required.");
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<MediaOptions>, MediaOptionsValidator>();
        services.AddDbContext<MediaDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsAssembly(typeof(MediaDbContext).Assembly.FullName).MigrationsHistoryTable(MediaPersistence.MigrationsHistoryTable, MediaPersistence.Schema)).UseSnakeCaseNamingConvention());
        services.AddHealthChecks().AddDbContextCheck<MediaDbContext>("media-postgresql", tags: ["ready"]);
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>(); services.AddSingleton<IMediaStorage, LocalMediaStorage>(); services.AddSingleton<IMediaImageProcessor, ImageSharpMediaProcessor>(); services.AddSingleton<IMediaMalwareScanner, NoOpMediaMalwareScanner>();
        services.AddScoped<MediaService>(); services.AddScoped<IMediaService>(p => p.GetRequiredService<MediaService>()); services.AddScoped<IMediaAssetLookup>(p => p.GetRequiredService<MediaService>()); services.AddScoped<IMediaCleanupService, MediaCleanupService>();
        return services;
    }
}
