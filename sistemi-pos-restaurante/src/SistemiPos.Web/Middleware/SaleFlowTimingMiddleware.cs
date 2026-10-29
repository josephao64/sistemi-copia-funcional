using System.Diagnostics;
using System.Globalization;

namespace SistemiPos.Web.Middleware;

public sealed class SaleFlowTimingMiddleware(RequestDelegate next, ILogger<SaleFlowTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/Sales") && !path.StartsWithSegments("/pos")) { await next(context); return; }
        var start = Stopwatch.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            var ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            context.Response.Headers.Append("Server-Timing", "app;dur=" + ms.ToString("F1", CultureInfo.InvariantCulture));
            return Task.CompletedTask;
        });
        await next(context);
        logger.LogInformation("Sale flow {Method} {Path} returned {Status} in {ElapsedMs} ms.",
            context.Request.Method, path.Value, context.Response.StatusCode, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
}
