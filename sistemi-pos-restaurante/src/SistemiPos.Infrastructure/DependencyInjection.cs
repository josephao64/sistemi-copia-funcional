using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemiPos.Application.Security;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Cash;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Inventory;
using SistemiPos.Application.Purchases;
using SistemiPos.Application.Customers;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Settings;
using SistemiPos.Application.Reports;
using SistemiPos.Infrastructure.Cash;
using SistemiPos.Infrastructure.Catalog;
using SistemiPos.Infrastructure.Customers;
using SistemiPos.Infrastructure.Inventory;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Persistence.Interceptors;
using SistemiPos.Infrastructure.Purchases;
using SistemiPos.Infrastructure.Security;
using SistemiPos.Infrastructure.Security.Authorization;
using SistemiPos.Infrastructure.Services;
using SistemiPos.Infrastructure.Sales;
using SistemiPos.Application.Suppliers;
using SistemiPos.Infrastructure.Suppliers;
using SistemiPos.Infrastructure.Settings;
using SistemiPos.Infrastructure.Reports;
using SistemiPos.Domain.Configuration;
using SistemiPos.Application.Exports;
using SistemiPos.Infrastructure.Exports;
using SistemiPos.Application.Finance;
using SistemiPos.Infrastructure.Finance;
using SistemiPos.Application.Organizations;
using SistemiPos.Infrastructure.Organizations;
using SistemiPos.Infrastructure.Seed;

namespace SistemiPos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton(provider => new CashAuthorizationPolicy(
            string.Equals(provider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase)
            && configuration.GetValue<bool>("PosCash:SkipAdditionalCredentialsForDevelopment")));
        services.AddOptions<PosCashOptions>().Bind(configuration.GetSection("PosCash"))
            .Validate(x => x.IsValid(), "La configuración PosCash contiene límites o identificadores inválidos.").ValidateOnStart();
        services.AddScoped<IPosCashService, PosCashService>();
        services.AddDataProtection();
        services.AddScoped<IOfflinePosProtector, OfflinePosProtector>();
        services.AddScoped<SistemiPos.Application.Orders.IOrderRepository, SistemiPos.Infrastructure.Orders.OrderRepository>();
        services.AddScoped<SistemiPos.Application.Orders.IOrderService, SistemiPos.Application.Orders.OrderService>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<UserAccountService>();
        services.AddScoped<LoginAuthorizationService>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' no configurada.");

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = LoginSecurityDefaults.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LoginSecurityDefaults.LockoutDuration;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<SignInManager<AppUser>>();

        services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(RoleAccessPolicies.SalesAccess, policy =>
            {
                policy.RequireAuthenticatedUser();
            });
        });

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IOperationAuthorizationService, OperationAuthorizationService>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IUnitConversionService, UnitConversionService>();
        services.AddScoped<SistemiPos.Application.Restaurant.IRestaurantRepository, SistemiPos.Infrastructure.Restaurant.RestaurantRepository>();
        services.AddScoped<SistemiPos.Application.Restaurant.IRestaurantService, SistemiPos.Application.Restaurant.RestaurantService>();
        services.AddScoped<SistemiPos.Application.Recipes.IRecipeRepository, SistemiPos.Infrastructure.Recipes.RecipeRepository>();
        services.AddScoped<SistemiPos.Application.Recipes.IRecipeOperationsRepository, SistemiPos.Infrastructure.Recipes.RecipeOperationsRepository>();
        services.AddScoped<SistemiPos.Application.Recipes.IRecipeOperationsService, SistemiPos.Application.Recipes.RecipeOperationsService>();
        services.AddScoped<SistemiPos.Application.Recipes.IRecipeService, SistemiPos.Application.Recipes.RecipeService>();
        services.AddScoped<IProductPricingService, ProductPricingService>();
        services.AddScoped<ICashRepository, CashRepository>();
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<INitLookupConfigurationProvider, NitLookupConfigurationProvider>();
        services.AddScoped<INitLookupService, NitLookupService>();
        services.AddSingleton<INitLookupProvider>(new UnconfiguredNitLookupProvider(NitLookupProviderType.Infile));
        services.AddSingleton<INitLookupProvider>(new UnconfiguredNitLookupProvider(NitLookupProviderType.Other));
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IControlReportService, ControlReportService>();
        services.AddSingleton<ITablePdfWriter, TablePdfWriter>();
        services.AddSingleton<IBackupService, PostgresBackupService>();
        services.AddHostedService<BackupWorker>();
        services.AddScoped<IFiscalService, FiscalService>();
        services.AddSingleton<IFiscalProvider, UnconfiguredFiscalProvider>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IPurchaseOrderPdfService, PurchaseOrderPdfService>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryLotService, InventoryLotService>();
        services.AddScoped<IInventoryQueryService, InventoryQueryService>();
        services.AddScoped<IInventoryTransferRepository, InventoryTransferRepository>();
        services.AddScoped<IInventoryTransferService, InventoryTransferService>();
        services.AddScoped<IPhysicalInventoryCountRepository, PhysicalInventoryCountRepository>();
        services.AddScoped<IPhysicalInventoryCountService, PhysicalInventoryCountService>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<ISaleReceiptService, SaleReceiptService>();
        services.Configure<SistemiPos.Infrastructure.Sales.PdfJobs.ReceiptQueueOptions>(configuration.GetSection("ReceiptQueue"));
        services.AddScoped<ISalePdfQueue, SistemiPos.Infrastructure.Sales.PdfJobs.SalePdfQueue>();
        services.AddScoped<SistemiPos.Infrastructure.Sales.PdfJobs.SalePdfProcessor>();
        services.AddHostedService<SistemiPos.Infrastructure.Sales.PdfJobs.SalePdfOutboxPublisher>();
        services.AddHostedService<SistemiPos.Infrastructure.Sales.PdfJobs.SalePdfConsumer>();
        services.AddScoped<IFinancialService, FinancialService>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ICompanySettingsRepository, CompanySettingsRepository>();
        services.AddScoped<IBranchSettingsRepository, BranchSettingsRepository>();
        services.AddScoped<IEnterpriseSettingsResolver, EnterpriseSettingsResolver>();
        services.AddScoped<SecurityBootstrapper>();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<CatalogDefaultsSeeder>();
        services.AddScoped<OrganizationDefaultsSeeder>();

        return services;
    }
}
