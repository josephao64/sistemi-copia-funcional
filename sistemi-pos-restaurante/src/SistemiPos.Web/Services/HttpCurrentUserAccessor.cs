using System.Security.Claims;
using SistemiPos.Application.Abstractions;

namespace SistemiPos.Web.Services;

public sealed class HttpCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public string UserName =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true
            ? httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Name) ?? "authenticated-user"
            : "anonymous";

    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
}
