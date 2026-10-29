using Microsoft.AspNetCore.Identity;
using SistemiPos.Application.Security;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Security;

public sealed class UserAccountService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    AppDbContext dbContext)
{
    public async Task<IdentityResult> ValidateRolesAsync(IReadOnlyCollection<string> roles)
    {
        if (!UserRoleSelection.IsValid(roles))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "SingleRoleRequired", Description = UserRoleSelection.ValidationMessage
            });
        }

        return await roleManager.RoleExistsAsync(roles.Single())
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError
            {
                Code = "RoleNotFound", Description = "El rol seleccionado no existe."
            });
    }

    public async Task<IdentityResult> SaveAsync(
        AppUser user, IReadOnlyCollection<string> roles, bool isNew, string? password = null)
    {
        var validation = await ValidateRolesAsync(roles);
        if (!validation.Succeeded) return validation;

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var result = isNew
            ? await userManager.CreateAsync(user, password!)
            : await userManager.UpdateAsync(user);
        if (!result.Succeeded) return result;

        var currentRoles = await userManager.GetRolesAsync(user);
        var selectedRole = roles.Single();
        var rolesToRemove = currentRoles
            .Where(role => !string.Equals(role, selectedRole, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (rolesToRemove.Count > 0)
        {
            result = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!result.Succeeded) return result;
        }

        if (!currentRoles.Contains(selectedRole, StringComparer.OrdinalIgnoreCase))
        {
            result = await userManager.AddToRoleAsync(user, selectedRole);
            if (!result.Succeeded) return result;
        }

        if (!isNew && (rolesToRemove.Count > 0 || currentRoles.Count == 0))
        {
            result = await userManager.UpdateSecurityStampAsync(user);
            if (!result.Succeeded) return result;
        }

        await transaction.CommitAsync();
        return IdentityResult.Success;
    }
}
