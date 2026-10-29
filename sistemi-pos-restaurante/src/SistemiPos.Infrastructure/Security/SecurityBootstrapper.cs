using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Security;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common;

namespace SistemiPos.Infrastructure.Security;

public sealed class SecurityBootstrapper(
    AppDbContext dbContext,
    RoleManager<AppRole> roleManager,
    UserManager<AppUser> userManager,
    IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var permissionsByCode = (await dbContext.Permissions.ToListAsync(cancellationToken))
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var permissionDefinition in PermissionCodes.All)
        {
            if (permissionsByCode.TryGetValue(permissionDefinition.Code, out var permission))
            {
                permission.Module = permissionDefinition.Module;
                permission.Action = permissionDefinition.Action;
                permission.Description = permissionDefinition.Description;
                continue;
            }

            permission = new Permission
            {
                Code = permissionDefinition.Code,
                Module = permissionDefinition.Module,
                Action = permissionDefinition.Action,
                Description = permissionDefinition.Description
            };
            dbContext.Permissions.Add(permission);
            permissionsByCode.Add(permission.Code, permission);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await SeedFinancialConfigurationAsync(cancellationToken);

        await EnsureRoleAsync(
            SecurityRoleNames.SuperAdministrator,
            "Acceso total y administración protegida del sistema.",
            PermissionCodes.All.Select(x => x.Code),
            enforcePermissions: true,
            cancellationToken);
        await EnsureRoleAsync(
            SecurityRoleNames.Administrator,
            "Administra la operación diaria sin modificar la protección del super administrador.",
            [
                PermissionCodes.DashboardView,
                PermissionCodes.UsersView, PermissionCodes.UsersManage, PermissionCodes.UsersResetPassword,
                PermissionCodes.RolesView, PermissionCodes.RolesManage, PermissionCodes.PermissionsView,
                PermissionCodes.SecurityAuditView,
                PermissionCodes.OrganizationsView, PermissionCodes.OrganizationsManage, PermissionCodes.BranchSwitch,
                PermissionCodes.CategoriesView, PermissionCodes.CategoriesManage, PermissionCodes.ProductsView, PermissionCodes.ProductsManage,
                PermissionCodes.RecipesView, PermissionCodes.RecipesManage,
                PermissionCodes.ProductionView, PermissionCodes.ProductionManage, PermissionCodes.ProductionConfirm,
                PermissionCodes.SuppliersView, PermissionCodes.SuppliersManage, PermissionCodes.PurchasesView, PermissionCodes.PurchasesManage,
                PermissionCodes.PurchasesConfirm, PermissionCodes.PurchasesCancel, PermissionCodes.InventoryView, PermissionCodes.InventoryEntry, PermissionCodes.InventoryExit, PermissionCodes.InventoryAdjust,
                PermissionCodes.InventoryTransfersView, PermissionCodes.InventoryTransfersCreate, PermissionCodes.InventoryTransfersDispatch, PermissionCodes.InventoryTransfersReceive,
                PermissionCodes.CustomersView, PermissionCodes.CustomersManage,
                PermissionCodes.CashView, PermissionCodes.CashManage,
                PermissionCodes.CashControlAccess, PermissionCodes.CashControlViewAll, PermissionCodes.CashControlViewOtherUsers,
                PermissionCodes.CashControlViewSales, PermissionCodes.CashControlViewMovements, PermissionCodes.CashControlViewCounts,
                PermissionCodes.CashControlViewPosClosings, PermissionCodes.CashControlViewDifferences, PermissionCodes.CashControlViewReports,
                PermissionCodes.CashMovementsView, PermissionCodes.CashMovementsAuthorize, PermissionCodes.CashDifferenceAuthorize,
                PermissionCodes.CashAuditAccess, PermissionCodes.CashAuditView, PermissionCodes.CashAuditViewAuthorizations,
                PermissionCodes.PettyCashViewBalance, PermissionCodes.PettyCashViewHistory, PermissionCodes.PettyCashReports,
                PermissionCodes.AccountsPayableView, PermissionCodes.AccountsPayablePay,
                PermissionCodes.AccountsReceivableView, PermissionCodes.AccountsReceivableCollect,
                PermissionCodes.DiscountsApply, PermissionCodes.DiscountsAuthorize, PermissionCodes.TransfersVerify, PermissionCodes.RefundsAuthorize,
                PermissionCodes.ReportsView,
                PermissionCodes.TablesView, PermissionCodes.TablesManage,
                PermissionCodes.OrdersView, PermissionCodes.OrdersCreate, PermissionCodes.OrdersUpdate,
                PermissionCodes.RestaurantConfigure, PermissionCodes.OrdersCancel, PermissionCodes.OrdersDeliver, PermissionCodes.DeliveryManage,
                PermissionCodes.KitchenOrdersView, PermissionCodes.KitchenOrdersStatus
            ],
            enforcePermissions: true,
            cancellationToken);
        await EnsureRoleAsync(
            SecurityRoleNames.Manager,
            "Supervisa la operación mediante consultas, caja y reportes.",
            [
                PermissionCodes.DashboardView, PermissionCodes.BranchSwitch, PermissionCodes.CashView, PermissionCodes.CashControlAccess,
                PermissionCodes.CashControlViewAll, PermissionCodes.CashControlViewOtherUsers, PermissionCodes.CashControlViewSales,
                PermissionCodes.CashControlViewMovements, PermissionCodes.CashControlViewCounts, PermissionCodes.CashControlViewDifferences,
                PermissionCodes.CashMovementsView, PermissionCodes.CashAuditAccess, PermissionCodes.CashAuditView,
                PermissionCodes.AccountsPayableView, PermissionCodes.AccountsReceivableView,
                PermissionCodes.ProductsView, PermissionCodes.CategoriesView, PermissionCodes.InventoryView, PermissionCodes.InventoryAdjust,
                PermissionCodes.InventoryTransfersView, PermissionCodes.InventoryTransfersCreate, PermissionCodes.InventoryTransfersDispatch, PermissionCodes.InventoryTransfersReceive,
                PermissionCodes.PurchasesView, PermissionCodes.SuppliersView, PermissionCodes.CustomersView, PermissionCodes.ReportsView,
                PermissionCodes.DiscountsAuthorize, PermissionCodes.UsersView,
                PermissionCodes.TablesView, PermissionCodes.OrdersView
            ],
            enforcePermissions: true,
            cancellationToken);
        await EnsureRoleAsync(
            SecurityRoleNames.PointOfSale,
            "Opera únicamente el punto de venta y su caja.",
            [PermissionCodes.SalesView, PermissionCodes.SalesManage, PermissionCodes.DiscountsApply, PermissionCodes.OrdersView,
                PermissionCodes.SalesCancel, PermissionCodes.SalesReturn,
                PermissionCodes.CustomersView, PermissionCodes.CashView, PermissionCodes.CashManage,
                PermissionCodes.CashOpen, PermissionCodes.CashCloseOwn, PermissionCodes.CashCount,
                PermissionCodes.CashMovementsAddIncome, PermissionCodes.CashMovementsAddExpense],
            enforcePermissions: true,
            cancellationToken);

        await EnsureRoleAsync(
            SecurityRoleNames.Cashier,
            "Registra cobros, emite comprobantes y opera su caja de ventas.",
            [
                PermissionCodes.SalesView, PermissionCodes.SalesManage, PermissionCodes.DiscountsApply,
                PermissionCodes.CustomersView, PermissionCodes.CashView, PermissionCodes.CashManage,
                PermissionCodes.CashOpen, PermissionCodes.CashCloseOwn, PermissionCodes.CashCount,
                PermissionCodes.CashMovementsAddIncome, PermissionCodes.CashMovementsAddExpense,
                PermissionCodes.OrdersView
            ],
            enforcePermissions: true,
            cancellationToken);

        await EnsureRoleAsync(
            SecurityRoleNames.Waiter,
            "Gestiona mesas, comanda pedidos, consulta menú y envía órdenes a cocina.",
            [
                PermissionCodes.OrdersView, PermissionCodes.OrdersCreate, PermissionCodes.OrdersUpdate,
                PermissionCodes.TablesView, PermissionCodes.TablesManage,
                PermissionCodes.OrdersDeliver,
                PermissionCodes.ProductsView, PermissionCodes.CategoriesView
            ],
            enforcePermissions: true,
            cancellationToken);

        await EnsureRoleAsync(
            SecurityRoleNames.Kitchen,
            "Consulta pedidos enviados a cocina y actualiza sus estados de preparación.",
            [
                PermissionCodes.KitchenOrdersView, PermissionCodes.KitchenOrdersStatus,
                PermissionCodes.OrdersView
            ],
            enforcePermissions: true,
            cancellationToken);

        await EnsureRoleAsync(
            SecurityRoleNames.Warehouse,
            "Gestiona entradas, salidas, existencias y movimientos de almacén e insumos.",
            [
                PermissionCodes.ProductsView, PermissionCodes.CategoriesView,
                PermissionCodes.InventoryView, PermissionCodes.InventoryEntry,
                PermissionCodes.InventoryExit, PermissionCodes.InventoryAdjust,
                PermissionCodes.InventoryTransfersView, PermissionCodes.InventoryTransfersCreate,
                PermissionCodes.InventoryTransfersDispatch, PermissionCodes.InventoryTransfersReceive
            ],
            enforcePermissions: true,
            cancellationToken);

        await RemoveUnusedLegacyRoleAsync(SecurityRoleNames.LegacyAdministrator, cancellationToken);
        await EnsureUserLockoutEnabledAsync(cancellationToken);

        var bootstrapSection = configuration.GetSection("Security:BootstrapAdmin");
        var envPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? configuration["ADMIN_PASSWORD"];
        var isBootstrapEnabled = bootstrapSection.GetValue<bool>("Enabled") || !string.IsNullOrWhiteSpace(envPassword);
        if (!isBootstrapEnabled)
        {
            return;
        }

        var legacyRoleName = bootstrapSection.GetValue<string>("RoleName") ?? SecurityRoleNames.LegacyAdministrator;
        var userName = Environment.GetEnvironmentVariable("ADMIN_USERNAME")
            ?? configuration["ADMIN_USERNAME"]
            ?? bootstrapSection.GetValue<string>("UserName")
            ?? "admin";
        var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL")
            ?? configuration["ADMIN_EMAIL"]
            ?? bootstrapSection.GetValue<string>("Email")
            ?? "admin@restaurante.local";
        var password = envPassword ?? bootstrapSection.GetValue<string>("Password");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Security:BootstrapAdmin:Password o ADMIN_PASSWORD es obligatorio cuando el bootstrap está habilitado.");
        }

        var fullName = Environment.GetEnvironmentVariable("ADMIN_FULLNAME")
            ?? configuration["ADMIN_FULLNAME"]
            ?? bootstrapSection.GetValue<string>("FullName")
            ?? "Administrador";

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = userName,
                Email = email,
                FullName = SistemiPos.Domain.Common.NameNormalizer.Normalize(fullName),
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return;
            }
        }
        else
        {
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, password);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            user.LockoutEnabled = true;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return;
            }
        }
        if (!await userManager.IsInRoleAsync(user, SecurityRoleNames.SuperAdministrator))
        {
            await userManager.AddToRoleAsync(user, SecurityRoleNames.SuperAdministrator);
        }

        if (!string.Equals(legacyRoleName, SecurityRoleNames.SuperAdministrator, StringComparison.OrdinalIgnoreCase) &&
            await userManager.IsInRoleAsync(user, legacyRoleName))
        {
            await userManager.RemoveFromRoleAsync(user, legacyRoleName);
        }

        await RemoveUnusedLegacyRoleAsync(legacyRoleName, cancellationToken);
        await RemoveUnusedLegacyRoleAsync(SecurityRoleNames.LegacyAdministrator, cancellationToken);
    }

    private async Task EnsureRoleAsync(
        string name,
        string description,
        IEnumerable<string> permissionCodes,
        bool enforcePermissions,
        CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(name);
        var isNew = role is null;
        if (role is null)
        {
            role = new AppRole { Name = name, Description = description };
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                return;
            }
        }

        var desiredIds = await dbContext.Permissions
            .Where(x => permissionCodes.Contains(x.Code))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var current = await dbContext.RolePermissions.Where(x => x.RoleId == role.Id).ToListAsync(cancellationToken);

        if (enforcePermissions)
        {
            dbContext.RolePermissions.RemoveRange(current.Where(x => !desiredIds.Contains(x.PermissionId)));
        }

        if (isNew || enforcePermissions || current.Count == 0)
        {
            var currentIds = current.Select(x => x.PermissionId).ToHashSet();
            foreach (var permissionId in desiredIds.Where(x => !currentIds.Contains(x)))
            {
                dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveUnusedLegacyRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleName) ||
            string.Equals(roleName, SecurityRoleNames.SuperAdministrator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, SecurityRoleNames.Administrator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, SecurityRoleNames.Manager, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, SecurityRoleNames.PointOfSale, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null || await dbContext.UserRoles.AnyAsync(x => x.RoleId == role.Id, cancellationToken))
        {
            return;
        }

        await roleManager.DeleteAsync(role);
    }

    private async Task EnsureUserLockoutEnabledAsync(CancellationToken cancellationToken)
    {
        var usersWithoutLockout = await userManager.Users
            .Where(x => !x.LockoutEnabled)
            .ToListAsync(cancellationToken);

        foreach (var user in usersWithoutLockout)
        {
            user.LockoutEnabled = true;
            await userManager.UpdateAsync(user);
        }
    }

    private async Task SeedFinancialConfigurationAsync(CancellationToken cancellationToken)
    {
        var denominations = await dbContext.CashDenominations.ToListAsync(cancellationToken);
        var desiredDenominations = new (DenominationKind Kind, decimal Value)[]
        {
            (DenominationKind.Banknote, 200m), (DenominationKind.Banknote, 100m),
            (DenominationKind.Banknote, 50m), (DenominationKind.Banknote, 20m),
            (DenominationKind.Banknote, 10m), (DenominationKind.Banknote, 5m),
            (DenominationKind.Coin, 1m), (DenominationKind.Coin, .50m),
            (DenominationKind.Coin, .25m), (DenominationKind.Coin, .10m),
            (DenominationKind.Coin, .05m)
        };
        foreach (var item in desiredDenominations)
        {
            if (denominations.Any(x => x.Kind == item.Kind && x.Value == item.Value))
            {
                continue;
            }

            var denomination = new CashDenomination(item.Kind, item.Value);
            dbContext.CashDenominations.Add(denomination);
            denominations.Add(denomination);
        }

        var oneQuetzalCoinIsActive = denominations.Any(
            x => x.Kind == DenominationKind.Coin && x.Value == 1m && x.IsActive);
        if (oneQuetzalCoinIsActive)
        {
            var duplicatedOneQuetzalBanknote = denominations.SingleOrDefault(
                x => x.Kind == DenominationKind.Banknote && x.Value == 1m && x.IsActive);
            duplicatedOneQuetzalBanknote?.Update(
                duplicatedOneQuetzalBanknote.Kind,
                duplicatedOneQuetzalBanknote.Value,
                false);
        }

        var methods = new[]
        {
            ("CASH", "Efectivo", PaymentFinancialBehavior.Cash),
            ("TRANSFER", "Transferencia", PaymentFinancialBehavior.Transfer),
            ("POS", "POS / Tarjeta", PaymentFinancialBehavior.Pos),
            ("CREDIT", "Crédito", PaymentFinancialBehavior.Credit)
        };
        var paymentMethodCodes = (await dbContext.PaymentMethodDefinitions
                .Select(x => x.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in methods)
            if (paymentMethodCodes.Add(item.Item1))
                dbContext.PaymentMethodDefinitions.Add(new PaymentMethodDefinition(item.Item1, item.Item2, item.Item3));

        var concepts = new[]
        {
            ("Fondo adicional", CashMovementType.OtherIncome), ("Ingreso adicional", CashMovementType.OtherIncome),
            ("Retiro de efectivo", CashMovementType.CashWithdrawal), ("Traslado", CashMovementType.CashTransfer),
            ("Pago proveedor", CashMovementType.SupplierPayment), ("Otro egreso", CashMovementType.Expense)
        };
        var existingConcepts = await dbContext.CashMovementConcepts.ToListAsync(cancellationToken);
        foreach (var item in concepts)
        {
            var concept = new CashMovementConcept(item.Item1, item.Item2);
            if (existingConcepts.Any(x =>
                    x.MovementType == concept.MovementType && NameNormalizer.Equals(x.Name, concept.Name)))
            {
                continue;
            }

            dbContext.CashMovementConcepts.Add(concept);
            existingConcepts.Add(concept);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
