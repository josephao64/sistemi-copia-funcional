using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Security;

public sealed class OperationAuthorizationService(UserManager<AppUser> userManager, AppDbContext dbContext,
    IOperationContextAccessor? context = null, IOrganizationRepository? organizations = null) : IOperationAuthorizationService
{
    public async Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null || !user.IsActive) return false;
        if (context?.CurrentBranchId is Guid branchId && organizations is not null)
        {
            var branches = await organizations.GetAuthorizedBranchesForUserAsync(user.Id,
                await userManager.IsInRoleAsync(user, SecurityRoleNames.SuperAdministrator), cancellationToken);
            if (branches.All(x => x.Id != branchId)) return false;
        }
        var roleIds = await dbContext.UserRoles.Where(x => x.UserId == user.Id).Select(x => x.RoleId).ToListAsync(cancellationToken);
        return await dbContext.RolePermissions.AnyAsync(x => roleIds.Contains(x.RoleId) && x.Permission.Code == permissionCode && x.Permission.IsActive, cancellationToken);
    }

    public async Task<string> ValidateCredentialsAsync(string userName, string password, string? requiredPermission = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            throw new DomainRuleException("Usuario y contraseña son requeridos.");
        var user = await userManager.FindByNameAsync(userName.Trim());
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, password))
            throw new DomainRuleException("Las credenciales proporcionadas no son válidas.");
        if (requiredPermission is not null && !await HasPermissionAsync(user.UserName!, requiredPermission, cancellationToken))
            throw new DomainRuleException("El usuario no tiene el permiso requerido para autorizar esta operación.");
        return user.UserName!;
    }
}
