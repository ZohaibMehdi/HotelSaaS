using HotelSaaS.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HotelSaaS.Infrastructure.Notifications;

/// <summary>
/// PRD section 35: notification providers are abstracted behind interfaces.
/// V1 writes to the structured log; replace with SMTP/SMS/WhatsApp providers later.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public void Send(string to, string subject, string body) =>
        logger.LogInformation("EMAIL -> To={To} Subject='{Subject}' Body={Body}", to, subject,
            body.Length > 500 ? body[..500] + "..." : body);
}
