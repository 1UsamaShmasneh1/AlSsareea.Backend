using System.Security.Cryptography;
using System.Text;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using Microsoft.AspNetCore.DataProtection;

namespace AlSsareea.Modules.Notifications.Infrastructure.Providers;

internal sealed class TokenProtector(IDataProtectionProvider provider) : ITokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("AlSsareea.Notifications.DeviceTokens.v1");
    public string Protect(string token) => _protector.Protect(token);
    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public string Mask(string token) => token.Length <= 8 ? "********" : $"{token[..4]}…{token[^4..]}";
}
internal sealed class InAppSender : INotificationChannelSender
{
    public string Provider => "inapp"; public NotificationChannel Channel => NotificationChannel.InApp;
    public Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken) => Task.FromResult(new ProviderSendResult(true, true, ProviderFailureKind.None, ProviderMessageId: request.DeliveryId.Value.ToString("N")));
}
internal sealed class UnavailableSender(string provider, NotificationChannel channel) : INotificationChannelSender
{
    public string Provider { get; } = provider; public NotificationChannel Channel { get; } = channel;
    public Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken) => Task.FromResult(new ProviderSendResult(false, false, ProviderFailureKind.NotConfigured, $"notifications.provider.{Provider}.not_configured"));
}

public interface IFcmPushAdapter { Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken); }
public interface IApnsPushAdapter { Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken); }
internal sealed class FcmSender(IFcmPushAdapter adapter) : INotificationChannelSender { public string Provider => "fcm"; public NotificationChannel Channel => NotificationChannel.Push; public Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken) => adapter.SendAsync(request, cancellationToken); }
internal sealed class ApnsSender(IApnsPushAdapter adapter) : INotificationChannelSender { public string Provider => "apns"; public NotificationChannel Channel => NotificationChannel.Push; public Task<ProviderSendResult> SendAsync(ProviderSendRequest request, CancellationToken cancellationToken) => adapter.SendAsync(request, cancellationToken); }
