using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using SistemiPos.Application.Security;

namespace SistemiPos.Web.Security;

internal static class CookieAuthenticationSessionExpired
{
    public static Task OnRedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (IsApiOrAjaxRequest(context.Request))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        var returnUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        var loginPath = context.Options.LoginPath.HasValue
            ? context.Options.LoginPath.Value!
            : "/Account/Login";

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReturnUrl"] = returnUrl
        };

        if (ShouldIndicateSessionExpired(context))
        {
            query[LoginSecurityDefaults.SessionExpiredQueryKey] = LoginSecurityDefaults.SessionExpiredQueryValue;
        }

        context.Response.Redirect(QueryHelpers.AddQueryString(loginPath, query));
        return Task.CompletedTask;
    }

    private static bool ShouldIndicateSessionExpired(RedirectContext<CookieAuthenticationOptions> context)
    {
        var cookieName = context.Options.Cookie.Name;
        if (!string.IsNullOrWhiteSpace(cookieName) &&
            context.Request.Cookies.ContainsKey(cookieName))
        {
            return true;
        }

        var refererHeader = context.Request.Headers.Referer.ToString();
        if (string.IsNullOrWhiteSpace(refererHeader) ||
            !Uri.TryCreate(refererHeader, UriKind.Absolute, out var referer))
        {
            return false;
        }

        if (!string.Equals(referer.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !referer.AbsolutePath.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase)
            && !referer.AbsolutePath.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsApiOrAjaxRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var accept = request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }
}
