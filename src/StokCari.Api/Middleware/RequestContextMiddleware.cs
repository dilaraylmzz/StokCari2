using Serilog.Context;
using StokCari.Core.Interfaces;

namespace StokCari.Api.Middleware;

/// <summary>Her isteğe bir Correlation ID atar, yanıt başlığına ve tüm log satırlarına ekler.</summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

/// <summary>Kimlik doğrulamadan sonra çalışır; UserId ve CompanyId bilgilerini log satırlarına ekler.</summary>
public class UserLogEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser)
    {
        using (LogContext.PushProperty("UserId", currentUser.UserId))
        using (LogContext.PushProperty("CompanyId", currentUser.CompanyId))
        {
            await next(context);
        }
    }
}
