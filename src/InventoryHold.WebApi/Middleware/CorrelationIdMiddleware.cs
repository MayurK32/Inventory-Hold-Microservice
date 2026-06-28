using Serilog.Context;

namespace InventoryHold.WebApi.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[Header].FirstOrDefault()
                 ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[Header] = id;
        using (LogContext.PushProperty("CorrelationId", id))
            await next(context);
    }
}
