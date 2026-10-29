using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Web.Services;

public sealed class HttpOperationContextAccessor(
    IHttpContextAccessor httpContextAccessor,
    IOrganizationRepository organizationRepository,
    ICurrentUserAccessor currentUserAccessor,
    INavigationPermissionService? navigationPermissionService = null) : IOperationContextAccessor
{
    private const string BranchCookieName = ".SistemiPosRestaurante.BranchContext";
    private Branch? _cachedBranch;
    private bool _resolved;
    private List<Branch>? _authorizedBranches;
    private (Guid UserId, bool IsSuperAdmin)? _authorizedScope;

    private async Task<List<Branch>> LoadAuthorizedBranchesAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default)
    {
        if (_authorizedBranches is not null && _authorizedScope == (userId, isSuperAdmin))
            return _authorizedBranches;
        var branches = await organizationRepository.GetAuthorizedBranchesForUserAsync(userId, isSuperAdmin, cancellationToken);
        _authorizedScope = (userId, isSuperAdmin);
        _authorizedBranches = branches;
        return branches;
    }

    private async Task EnsureResolvedAsync()
    {
        if (_resolved) return;
        _resolved = true;

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = currentUserAccessor.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return;
        }

        var isSuperAdmin = httpContext.User.IsInRole(SecurityRoleNames.SuperAdministrator);
        var canSwitchBranch = isSuperAdmin;
        if (!canSwitchBranch && navigationPermissionService is not null)
        {
            var permissions = await navigationPermissionService.GetPermissionCodesAsync();
            canSwitchBranch = permissions.Contains(PermissionCodes.BranchSwitch);
        }

        var authorizedBranches = await LoadAuthorizedBranchesAsync(userId.Value, isSuperAdmin);
        if (authorizedBranches.Count == 0)
        {
            return;
        }

        Guid? targetBranchId = null;
        if (canSwitchBranch &&
            httpContext.Request.Cookies.TryGetValue(BranchCookieName, out var cookieValue) &&
            Guid.TryParse(cookieValue, out var parsedBranchId))
        {
            if (authorizedBranches.Any(x => x.Id == parsedBranchId))
            {
                targetBranchId = parsedBranchId;
            }
        }

        if (!targetBranchId.HasValue)
        {
            var userScopes = await organizationRepository.GetUserScopesAsync(userId.Value);
            var defaultScope = userScopes.FirstOrDefault(x => x.IsDefault && x.BranchId.HasValue);
            if (defaultScope?.BranchId is not null && authorizedBranches.Any(x => x.Id == defaultScope.BranchId.Value))
            {
                targetBranchId = defaultScope.BranchId.Value;
            }
            else
            {
                targetBranchId = authorizedBranches.FirstOrDefault()?.Id;
            }

            if (targetBranchId.HasValue)
            {
                httpContext.Response.Cookies.Append(BranchCookieName, targetBranchId.Value.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = httpContext.Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            }
        }

        if (targetBranchId.HasValue)
        {
            _cachedBranch = authorizedBranches.FirstOrDefault(x => x.Id == targetBranchId.Value);
        }
    }

    public Guid? CurrentBranchId
    {
        get
        {
            EnsureResolvedAsync().GetAwaiter().GetResult();
            return _cachedBranch?.Id;
        }
    }

    public Guid? CurrentCompanyId
    {
        get
        {
            EnsureResolvedAsync().GetAwaiter().GetResult();
            return _cachedBranch?.CompanyId;
        }
    }

    public Guid? CurrentBusinessGroupId
    {
        get
        {
            EnsureResolvedAsync().GetAwaiter().GetResult();
            return _cachedBranch?.Company?.BusinessGroupId;
        }
    }

    public Guid? CurrentWarehouseId
    {
        get
        {
            EnsureResolvedAsync().GetAwaiter().GetResult();
            if (_cachedBranch is null) return null;
            var defaultWarehouse = _cachedBranch.Warehouses.FirstOrDefault(x => x.IsDefault && x.IsActive)
                ?? _cachedBranch.Warehouses.FirstOrDefault(x => x.IsActive);
            return defaultWarehouse?.Id;
        }
    }

    public string? CurrentBranchName
    {
        get
        {
            EnsureResolvedAsync().GetAwaiter().GetResult();
            return _cachedBranch?.Name;
        }
    }

    public string? CurrentCompanyName
    {
        get
        {
            EnsureResolvedAsync().GetAwaiter().GetResult();
            return _cachedBranch?.Company?.TradeName;
        }
    }

    public async Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var userId = currentUserAccessor.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return [];
        }

        var isSuperAdmin = httpContext.User.IsInRole(SecurityRoleNames.SuperAdministrator);
        var branches = await LoadAuthorizedBranchesAsync(userId.Value, isSuperAdmin, cancellationToken);
        return branches.Select(x => new BranchDto(
            x.Id,
            x.CompanyId,
            x.Company?.TradeName ?? string.Empty,
            x.Code,
            x.Name,
            x.Address,
            x.Phone,
            x.TimeZone,
            x.EstablishmentCode,
            x.IsActive,
            x.Warehouses.Count)).ToList();
    }

    public async Task SetCurrentBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No hay contexto HTTP activo.");

        var userId = currentUserAccessor.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var isSuperAdmin = httpContext.User.IsInRole(SecurityRoleNames.SuperAdministrator);
        if (!isSuperAdmin && navigationPermissionService is not null)
        {
            var permissions = await navigationPermissionService.GetPermissionCodesAsync(cancellationToken);
            if (!permissions.Contains(PermissionCodes.BranchSwitch))
            {
                throw new UnauthorizedAccessException("No tiene permiso para cambiar la sucursal de trabajo.");
            }
        }

        var authorizedBranches = await LoadAuthorizedBranchesAsync(userId, isSuperAdmin, cancellationToken);

        var target = authorizedBranches.FirstOrDefault(x => x.Id == branchId)
            ?? throw new UnauthorizedAccessException("No tiene autorización para operar en la sucursal seleccionada.");

        httpContext.Response.Cookies.Append(BranchCookieName, target.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        _cachedBranch = target;
        _resolved = true;
    }
}
