namespace SistemiPos.Application.Security;

public static class SecurityRoleNames
{
    public const string SuperAdministrator = "Super Administrador";
    public const string Administrator = "Administrador";
    public const string Manager = "Gerente";
    public const string PointOfSale = "Punto de Venta";
    public const string Cashier = "Cajero";
    public const string Waiter = "Mesero";
    public const string Kitchen = "Cocina";
    public const string Warehouse = "Bodega";
    public const string PointOfSaleAccess = PointOfSale + "," + Cashier + "," + SuperAdministrator;
    public const string LegacyAdministrator = "Administrator";
}
