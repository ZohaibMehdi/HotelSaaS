using System.Text.Json;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

/// <summary>Append-only audit trail writer/reader (PRD section 36).</summary>
public class AuditService(IAppDbContext db, ICurrentUser currentUser) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task LogAsync(string action, string entityName, object? entityId, object? oldValues = null,
        object? newValues = null, long? tenantId = null, CancellationToken ct = default)
    {
        try // auditing must never break the business operation
        {
            db.AuditLogs.Add(new Domain.Entities.AuditLog
            {
                TenantId = currentUser.IsSuperAdmin ? (currentUser.TenantId ?? tenantId) : currentUser.TenantId ?? tenantId,
                UserId = currentUser.UserId,
                UserName = currentUser.UserName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId?.ToString(),
                OldValues = oldValues == null ? null : JsonSerializer.Serialize(oldValues, JsonOpts),
                NewValues = newValues == null ? null : JsonSerializer.Serialize(newValues, JsonOpts),
                IpAddress = currentUser.IpAddress,
                CorrelationId = currentUser.CorrelationId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            // Swallow: audit failures are logged by infrastructure logging, not surfaced.
        }
    }

    public async Task<object> SearchAsync(long? tenantIdFilter, string? entityName, long? userId,
        DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        var scope = TenantScope.Optional(currentUser, tenantIdFilter);
        var q = db.AuditLogs.AsNoTracking();
        if (scope.HasValue) q = q.Where(a => a.TenantId == scope.Value);
        if (!string.IsNullOrWhiteSpace(entityName)) q = q.Where(a => a.EntityName == entityName);
        if (userId.HasValue) q = q.Where(a => a.UserId == userId.Value);
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedAt < to.Value.AddDays(1));
        q = q.OrderByDescending(a => a.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}

public class NotificationService(IAppDbContext db, IEmailSender emailSender) : INotificationService
{
    public Task NotifyUserAsync(long userId, long? tenantId, string title, string body,
        NotificationType type, CancellationToken ct = default) =>
        CreateInAppAsync(userId, tenantId, title, body, type, ct);

    public async Task CreateInAppAsync(long? userId, long? tenantId, string title, string body,
        NotificationType type, CancellationToken ct = default)
    {
        var n = new Domain.Entities.Notification
        {
            TenantId = tenantId, UserId = userId, Title = title, Body = body,
            Type = type, Channel = NotificationChannel.InApp, Status = NotificationStatus.Sent,
            SentAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>V1 email provider is a structured log sink; swap IEmailSender for production providers.</summary>
    public void SendEmail(string to, string subject, string body) => emailSender.Send(to, subject, body);
}
