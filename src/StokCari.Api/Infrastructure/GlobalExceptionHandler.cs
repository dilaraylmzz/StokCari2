using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StokCari.Core.Exceptions;

namespace StokCari.Api.Infrastructure;

/// <summary>
/// Tüm exception'ları RFC 7807 (Problem Details) yanıtına çevirir. Kullanıcıya iç detay sızdırılmaz;
/// correlationId ile loglardaki tam kayıt bulunur.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier;

        var (status, title, detail) = Map(exception);

        if (status >= 500)
            logger.LogError(exception, "Beklenmeyen hata. CorrelationId: {CorrelationId}", correlationId);
        else
            logger.LogWarning("İstek reddedildi ({Status}): {Detail}", status, detail);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    private static (int Status, string Title, string Detail) Map(Exception exception) => exception switch
    {
        AppException app => (app.StatusCode, app.Title, app.Message),

        DbUpdateConcurrencyException => (409, "Eşzamanlılık çakışması",
            "Kayıt başka bir işlem tarafından değiştirildi, lütfen tekrar deneyin."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (409, "Kayıt zaten mevcut",
            "Benzersiz olması gereken bir alan başka bir kayıtla çakışıyor."),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } => (409, "İlişkili kayıt hatası",
            "İşlem ilişkili bir kayıt nedeniyle yapılamadı."),

        _ => (500, "Beklenmeyen bir hata oluştu",
            "İşlem tamamlanamadı. Destek ile görüşürken correlationId değerini iletin.")
    };
}
