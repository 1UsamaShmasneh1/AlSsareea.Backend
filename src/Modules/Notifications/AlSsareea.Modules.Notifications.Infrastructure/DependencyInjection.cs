using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Notifications.Infrastructure.Persistence;
using AlSsareea.Modules.Notifications.Infrastructure.Processing;
using AlSsareea.Modules.Notifications.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlSsareea.Modules.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connection = configuration.GetConnectionString(NotificationsPersistence.ConnectionStringName); if (string.IsNullOrWhiteSpace(connection)) connection = configuration.GetConnectionString("DispatchingDatabase"); if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("ConnectionStrings:NotificationsDatabase is required.");
        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(connection, n => n.MigrationsAssembly(typeof(NotificationsDbContext).Assembly.FullName).MigrationsHistoryTable(NotificationsPersistence.MigrationsHistoryTable, NotificationsPersistence.Schema)).UseSnakeCaseNamingConvention()); services.AddHealthChecks().AddDbContextCheck<NotificationsDbContext>("notifications-postgresql", tags: ["ready"]);
        services.AddDataProtection(); services.AddOptions<NotificationProcessingOptions>().Bind(configuration.GetSection(NotificationProcessingOptions.SectionName)).Validate(x => x.PollingSeconds is >= 1 and <= 300 && x.BatchSize is >= 1 and <= 500 && x.DeliveryLimitPerMinute is >= 1 and <= 10_000 && x.MaximumBackoffSeconds is >= 5 and <= 86_400 && x.ProcessingLeaseSeconds is >= 30 and <= 3_600, "Notifications processing settings are outside their supported bounds.").ValidateOnStart(); services.AddOptions<FcmProviderOptions>().Bind(configuration.GetSection(FcmProviderOptions.SectionName)).Validate(x => x.IsValid(), "FCM settings are incomplete or outside their supported bounds.").ValidateOnStart(); services.AddOptions<ApnsProviderOptions>().Bind(configuration.GetSection(ApnsProviderOptions.SectionName)).Validate(x => x.IsValid(), "APNs settings are incomplete or outside their supported bounds.").ValidateOnStart(); services.AddScoped<INotificationStore, NotificationStore>(); services.AddScoped<INotificationService, NotificationService>(); services.AddSingleton<ITemplateRenderer, SafeTemplateRenderer>(); services.AddSingleton<ITokenProtector, TokenProtector>();
        services.AddSingleton<FcmCredentialCache>(); services.AddSingleton<ApnsJwtCache>(); services.AddHttpClient<FcmPushAdapter>(client => client.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers(); services.AddHttpClient<ApnsPushAdapter>(client => client.Timeout = Timeout.InfiniteTimeSpan).RemoveAllLoggers(); services.AddTransient<IFcmPushAdapter>(provider => provider.GetRequiredService<FcmPushAdapter>()); services.AddTransient<IApnsPushAdapter>(provider => provider.GetRequiredService<ApnsPushAdapter>()); services.AddTransient<INotificationChannelSender, FcmSender>(); services.AddTransient<INotificationChannelSender, ApnsSender>(); services.AddSingleton<INotificationChannelSender, InAppSender>(); services.AddSingleton<INotificationChannelSender>(new UnavailableSender("push-unavailable", NotificationChannel.Push)); services.AddSingleton<INotificationChannelSender>(new UnavailableSender("sms", NotificationChannel.Sms)); services.AddSingleton<INotificationChannelSender>(new UnavailableSender("email", NotificationChannel.Email)); services.AddSingleton<INotificationChannelSender>(new UnavailableSender("whatsapp", NotificationChannel.WhatsApp));
        services.AddScoped<IIntegrationEventConsumer, SourceNotificationConsumer>(); services.AddScoped<IIntegrationEventDispatcher, IntegrationEventDispatcher>(); services.AddScoped<INotificationDeliveryProcessor, NotificationDeliveryProcessor>(); services.AddHostedService<IntegrationOutboxWorker>(); services.AddHostedService<NotificationDeliveryWorker>(); return services;
    }
}
