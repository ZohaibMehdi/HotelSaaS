using HotelSaaS.Application.Common;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ValidationException = FluentValidation.ValidationException;

namespace HotelSaaS.Api.Middleware;

/// <summary>
/// Global exception handler producing the consistent error envelope (PRD section 51).
/// Raw exception details are never returned outside Development (PRD section 44).
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, message, errors) = MapException(ex);
            if (status >= 500)
                logger.LogError(ex, "Unhandled exception on {Method} {Path} [CorrelationId={CorrelationId}]",
                    context.Request.Method, context.Request.Path, context.Items["CorrelationId"]);
            else
                logger.LogWarning("{ExceptionType} on {Method} {Path}: {Message}",
                    ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new { success = false, message, errors }, JsonOpts);
            await context.Response.WriteAsync(payload);
        }
    }

    private (int Status, string Message, object? Errors) MapException(Exception ex) => ex switch
    {
        ValidationException v => (400, "Validation failed.",
            (object?)v.Errors.GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
        AppException a => (a.StatusCode, a.Message, a.Errors),
        DbUpdateConcurrencyException => (409, "The record was modified by another user. Please retry.", null),
        DbUpdateException db when db.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
                                  sql.Number is 2601 or 2627 =>
            (409, "A record with the same unique value already exists.", null),
        _ => (500, env.IsDevelopment() ? ex.Message : "An unexpected error occurred. Please try again.", null)
    };
}
