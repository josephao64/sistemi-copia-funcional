using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class RestaurantSecurityTests
{
    [Fact]
    public void Restaurant_Roles_Are_Properly_Defined()
    {
        Assert.Equal("Administrador", SecurityRoleNames.Administrator);
        Assert.Equal("Gerente", SecurityRoleNames.Manager);
        Assert.Equal("Cajero", SecurityRoleNames.Cashier);
        Assert.Equal("Mesero", SecurityRoleNames.Waiter);
        Assert.Equal("Cocina", SecurityRoleNames.Kitchen);
        Assert.Equal("Bodega", SecurityRoleNames.Warehouse);
    }

    [Fact]
    public void Restaurant_Permissions_Are_Registered_In_All_List()
    {
        var registeredCodes = PermissionCodes.All.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(PermissionCodes.SecurityAuditView, registeredCodes);
        Assert.Contains(PermissionCodes.UsersResetPassword, registeredCodes);
        Assert.Contains(PermissionCodes.TablesView, registeredCodes);
        Assert.Contains(PermissionCodes.TablesManage, registeredCodes);
        Assert.Contains(PermissionCodes.OrdersView, registeredCodes);
        Assert.Contains(PermissionCodes.OrdersCreate, registeredCodes);
        Assert.Contains(PermissionCodes.OrdersUpdate, registeredCodes);
        Assert.Contains(PermissionCodes.KitchenOrdersView, registeredCodes);
        Assert.Contains(PermissionCodes.KitchenOrdersStatus, registeredCodes);
        Assert.Contains(PermissionCodes.InventoryEntry, registeredCodes);
        Assert.Contains(PermissionCodes.InventoryExit, registeredCodes);
        Assert.Contains(PermissionCodes.InventoryAdjust, registeredCodes);
    }

    [Fact]
    public void Lockout_Settings_Meet_Security_Requirements()
    {
        Assert.Equal(5, LoginSecurityDefaults.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), LoginSecurityDefaults.LockoutDuration);
        Assert.False(string.IsNullOrWhiteSpace(LoginSecurityDefaults.InvalidCredentialsMessage));
        Assert.False(string.IsNullOrWhiteSpace(LoginSecurityDefaults.TemporaryLockoutMessage));
    }
}

