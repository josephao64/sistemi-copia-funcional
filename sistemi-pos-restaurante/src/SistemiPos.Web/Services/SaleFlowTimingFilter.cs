using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SistemiPos.Web.Services;

public sealed class SaleFlowTimingFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        if (controller is not ("Sales" or "PosCash")) { await next(); return; }
        var start = Stopwatch.GetTimestamp();
        await next();
        if (!context.HttpContext.Response.HasStarted)
            context.HttpContext.Response.Headers.Append("Server-Timing", "action;dur=" +
                Stopwatch.GetElapsedTime(start).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture));
    }
}
