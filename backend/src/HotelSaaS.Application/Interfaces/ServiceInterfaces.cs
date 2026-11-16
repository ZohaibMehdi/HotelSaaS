using HotelSaaS.Domain;

namespace HotelSaaS.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string entityName, object? entityId,
        object? oldValues = null, object? newValues = null, long? tenantId = null, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyUserAsync(long userId, long? tenantId, string title, string body,
        NotificationType type, CancellationToken ct = default);
    Task CreateInAppAsync(long? userId, long? tenantId, string title, string body,
        NotificationType type, CancellationToken ct = default);
    void SendEmail(string to, string subject, string body);
}

public interface IEmailSender
{
    /// <summary>V1 provider: structured log sink. Swap implementation to integrate a real SMTP/API provider.</summary>
    void Send(string to, string subject, string body);
}
