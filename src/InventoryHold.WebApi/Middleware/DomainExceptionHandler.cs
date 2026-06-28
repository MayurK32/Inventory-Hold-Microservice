using InventoryHold.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InventoryHold.WebApi.Middleware;

public sealed class DomainExceptionHandler(
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var pd = exception switch
        {
            InsufficientStockException e => Pd(409, "Insufficient stock", new { failures = e.Failures }),
            ProductNotFoundException e   => Pd(404, e.Message),
            HoldNotFoundException e      => Pd(404, e.Message),
            HoldTerminatedException e    => Pd(410, e.Message, new { at = e.At }),
            StockUnavailableException e  => Pd(409, e.Message),
            DomainException e           => Pd(422, e.Message),
            _                           => null
        };

        if (pd is null) return false;

        logger.LogWarning(exception,
            "Domain exception {ExceptionType} → HTTP {Status}: {Title}",
            exception.GetType().Name, pd.Status, pd.Title);

        context.Response.StatusCode = pd.Status!.Value;
        await context.Response.WriteAsJsonAsync(pd, ct);
        return true;
    }

    private static ProblemDetails Pd(int status, string title, object? data = null)
    {
        var pd = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.com/{status}"
        };
        if (data is not null) pd.Extensions["data"] = data;
        return pd;
    }
}
