namespace HotelSaaS.Api.Middleware;

/// <summary>
/// Structured request logging with correlation ids (PRD section 52).
/// Never logs bodies - therefore never logs passwords/tokens.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N")[..16];
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "-";
            var tenantId = context.User.FindFirst("tenantId")?.Value ?? "-";
            logger.LogInformation(
                "{StatusCode} {Method} {Path} in {ElapsedMs}ms | user={UserId} tenant={TenantId} corr={CorrelationId}",
                context.Response.StatusCode, context.Request.Method, context.Request.Path,
                sw.ElapsedMilliseconds, userId, tenantId, correlationId);
        }
    }
}
