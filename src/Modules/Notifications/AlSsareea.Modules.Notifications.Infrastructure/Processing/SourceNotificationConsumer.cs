using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlSsareea.BuildingBlocks.Application;
using AlSsareea.Modules.Customers.Contracts;
using AlSsareea.Modules.Delivery.Contracts;
using AlSsareea.Modules.Dispatching.Contracts;
using AlSsareea.Modules.Drivers.Contracts;
using AlSsareea.Modules.Merchants.Contracts;
using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Domain;
using AlSsareea.Modules.Orders.Contracts;

namespace AlSsareea.Modules.Notifications.Infrastructure.Processing;

internal sealed class SourceNotificationConsumer(INotificationService notifications, ICustomerNotificationRecipientProvider customers, IMerchantNotificationRecipientProvider merchants, IDriverNotificationRecipientProvider drivers, IDeliveryNotificationRecipientProvider deliveries) : IIntegrationEventConsumer
{
    private static readonly NotificationChannel[] DefaultChannels = [NotificationChannel.InApp, NotificationChannel.Push];
    public bool CanHandle(string source, string eventType) => source is "orders" or "delivery" or "dispatching";
    public async Task HandleAsync(OutboxMessageEnvelope message, CancellationToken ct)
    {
        if (message.Source == "orders" && message.EventType == nameof(OrderCreatedIntegrationEvent)) { await OrderCreated(message, Deserialize<OrderCreatedIntegrationEvent>(message), ct); return; }
        if (message.Source == "delivery" && message.EventType == nameof(DeliveryStatusChangedIntegrationEvent)) { await DeliveryChanged(message, Deserialize<DeliveryStatusChangedIntegrationEvent>(message), ct); return; }
        if (message.Source == "dispatching" && message.EventType == nameof(DispatchOfferCreatedIntegrationEvent)) await DispatchOffer(message, Deserialize<DispatchOfferCreatedIntegrationEvent>(message), ct);
    }
    private async Task OrderCreated(OutboxMessageEnvelope envelope, OrderCreatedIntegrationEvent value, CancellationToken ct)
    {
        CustomerNotificationRecipient? customer = await customers.GetAsync(value.CustomerId, ct);
        if (customer is not null) await Consume(envelope, customer.UserId, customer.Language, "order_updates", "order.created.customer", new Dictionary<string, string> { ["orderNumber"] = value.OrderNumber }, value.OccurredAtUtc, ct);
        foreach (MerchantNotificationRecipient merchant in await merchants.GetAsync(value.MerchantId, value.BranchId, ct)) await Consume(envelope, merchant.UserId, merchant.Language, "merchant_orders", "order.created.merchant", new Dictionary<string, string> { ["orderNumber"] = value.OrderNumber }, value.OccurredAtUtc, ct);
    }
    private async Task DeliveryChanged(OutboxMessageEnvelope envelope, DeliveryStatusChangedIntegrationEvent value, CancellationToken ct)
    {
        DeliveryNotificationRecipient? recipient = await deliveries.GetAsync(value.DeliveryId, ct); if (recipient is null) return;
        await Consume(envelope, recipient.UserId, recipient.Language, "order_updates", "delivery.status.customer", new Dictionary<string, string> { ["orderId"] = value.OrderId.ToString() }, value.OccurredAtUtc, ct);
    }
    private async Task DispatchOffer(OutboxMessageEnvelope envelope, DispatchOfferCreatedIntegrationEvent value, CancellationToken ct)
    {
        DriverNotificationRecipient? recipient = await drivers.GetAsync(value.DriverId, ct); if (recipient is null) return;
        await Consume(envelope, recipient.UserId, recipient.Language, "dispatch_offers", "dispatch.offer.driver", new Dictionary<string, string>(), value.OccurredAtUtc, ct);
    }
    private Task<bool> Consume(OutboxMessageEnvelope envelope, Guid userId, string language, string category, string key, IReadOnlyDictionary<string, string> parameters, DateTime occurred, CancellationToken ct) => notifications.ConsumeAsync(envelope.EventType, new SourceNotification(SubEventId(envelope.Id, userId, category), userId, language, category, key, parameters, DefaultChannels, occurred), ct);
    private static T Deserialize<T>(OutboxMessageEnvelope message) => JsonSerializer.Deserialize<T>(message.Payload) ?? throw new JsonException($"Unable to deserialize {message.EventType}.");
    private static Guid SubEventId(Guid eventId, Guid userId, string category) { byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{eventId:N}:{userId:N}:{category}")); return new(hash.AsSpan(0, 16)); }
}
