using AlSsareea.Modules.Notifications.Application;
using AlSsareea.Modules.Notifications.Contracts;
using AlSsareea.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace AlSsareea.Modules.Notifications.Infrastructure.Persistence;

internal sealed class NotificationStore(NotificationsDbContext db) : INotificationStore
{
    public async Task<NotificationListResponse> ListAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        IQueryable<Notification> query = db.Notifications.AsNoTracking().Where(x => x.UserId == userId && x.Channel == NotificationChannel.InApp); int total = await query.CountAsync(ct); int unread = await query.CountAsync(x => x.ReadAtUtc == null, ct);
        NotificationListItem[] items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new NotificationListItem(x.Id.Value, x.Category, x.TemplateKey, (short)x.Channel, x.Language, x.Subject, x.Body, (short)x.Status, x.CreatedAtUtc, x.ReadAtUtc)).ToArrayAsync(ct); return new(items, page, pageSize, total, unread);
    }
    public Task<Notification?> FindOwnedAsync(NotificationId id, Guid userId, CancellationToken ct) => db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.Channel == NotificationChannel.InApp, ct);
    public async Task<int> MarkAllReadAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
        int updated = await db.Notifications.Where(x => x.UserId == userId && x.Channel == NotificationChannel.InApp && x.ReadAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now).SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.ConcurrencyStamp, Guid.NewGuid()), ct);
        db.AuditRecords.Add(NotificationAuditRecord.Create(userId, "mark_all_read", "recipient_notifications", userId, $"count={updated}", now)); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return updated;
    }
    public Task<DeviceToken?> FindTokenAsync(DeviceTokenId id, Guid userId, CancellationToken ct) => db.DeviceTokens.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
    public Task<DeviceToken?> FindTokenByHashAsync(string hash, CancellationToken ct) => db.DeviceTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public async Task<IReadOnlyList<DeviceToken>> ActiveTokensAsync(Guid userId, CancellationToken ct) => await db.DeviceTokens.Where(x => x.UserId == userId && x.IsActive).ToArrayAsync(ct);
    public async Task<IReadOnlyList<NotificationPreference>> PreferencesAsync(Guid userId, CancellationToken ct) => await db.Preferences.Where(x => x.UserId == userId).ToArrayAsync(ct);
    public Task<NotificationTemplate?> FindTemplateAsync(string key, NotificationChannel channel, string language, CancellationToken ct) => db.Templates.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key && x.Channel == channel && x.Language == language && x.IsActive, ct);
    public Task<bool> SourceEventProcessedAsync(Guid eventId, CancellationToken ct) => db.InboxMessages.AnyAsync(x => x.Id == eventId, ct);
    public void Add(Notification value) => db.Notifications.Add(value);
    public void Add(DeviceToken value) => db.DeviceTokens.Add(value);
    public void Add(NotificationPreference value) => db.Preferences.Add(value);
    public void AddInbox(Guid eventId, string eventType, DateTime occurredAtUtc, DateTime processedAtUtc) => db.InboxMessages.Add(NotificationInboxMessage.Create(eventId, eventType, occurredAtUtc, processedAtUtc));
    public void AddAudit(Guid userId, string operation, string entityType, Guid entityId, string? detail, DateTime now) => db.AuditRecords.Add(NotificationAuditRecord.Create(userId, operation, entityType, entityId, detail, now));
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
