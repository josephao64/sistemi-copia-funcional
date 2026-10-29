using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Security;

namespace SistemiPos.Infrastructure.Seed;

public sealed class OrganizationDefaultsSeeder(
    AppDbContext db,
    UserManager<AppUser> userManager)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var systemSetting = await db.SystemSettings.FirstOrDefaultAsync(cancellationToken);

        // 1. Asegurar grupo empresarial predeterminado
        var defaultGroup = await db.BusinessGroups.FirstOrDefaultAsync(cancellationToken);
        if (defaultGroup is null)
        {
            defaultGroup = new BusinessGroup(
                code: "GRP-01",
                name: "Grupo Corporativo SISTEMI",
                legalName: "SISTEMI Alimentos Corporativos S.A.",
                taxNumber: "101085257",
                description: "Grupo empresarial principal del sistema");

            await db.BusinessGroups.AddAsync(defaultGroup, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 2. Asegurar empresa predeterminada
        var defaultCompany = await db.Companies.FirstOrDefaultAsync(x => x.BusinessGroupId == defaultGroup.Id, cancellationToken);
        if (defaultCompany is null)
        {
            var tradeName = !string.IsNullOrWhiteSpace(systemSetting?.BusinessName)
                ? systemSetting.BusinessName
                : "SISTEMI Restaurante";
            var nit = systemSetting?.BusinessNit ?? "101085257";
            var address = systemSetting?.BusinessAddress ?? "Jalapa, Guatemala";
            var phone = systemSetting?.BusinessPhone;
            var email = systemSetting?.BusinessEmail;

            defaultCompany = new Company(
                businessGroupId: defaultGroup.Id,
                code: "EMP-01",
                tradeName: tradeName,
                legalName: systemSetting?.BusinessLegalName ?? $"{tradeName} S.A.",
                nit: nit,
                email: email,
                phone: phone,
                address: address,
                taxRegime: "Régimen General");

            await db.Companies.AddAsync(defaultCompany, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Asegurar configuración para empresas sin ajustes registrados
        var companiesWithoutSettings = await db.Companies
            .Where(c => !db.CompanySettings.Any(cs => cs.CompanyId == c.Id))
            .ToListAsync(cancellationToken);

        foreach (var company in companiesWithoutSettings)
        {
            var companySetting = new CompanySetting(
                company.Id,
                businessName: company.TradeName,
                businessNit: company.Nit,
                businessAddress: company.Address,
                businessPhone: company.Phone,
                businessEmail: company.Email,
                businessLegalName: company.LegalName,
                businessLogoUrl: systemSetting?.BusinessLogoUrl,
                currencyCode: systemSetting?.CurrencyCode ?? "GTQ",
                currencyName: systemSetting?.CurrencyName ?? "Quetzal",
                currencySymbol: systemSetting?.CurrencySymbol ?? "Q",
                currencyDecimals: systemSetting?.CurrencyDecimals ?? 2,
                decimalSeparator: systemSetting?.DecimalSeparator ?? ".",
                thousandsSeparator: systemSetting?.ThousandsSeparator ?? ",",
                currencySymbolPosition: systemSetting?.CurrencySymbolPosition ?? CurrencySymbolPosition.Before,
                taxPercent: systemSetting?.TaxPercent ?? 12m,
                showTaxOnReceipt: systemSetting?.ShowTaxOnReceipt ?? true,
                receiptFooter: systemSetting?.ReceiptFooter ?? "¡Gracias por su compra!",
                cashPaymentsEnabled: systemSetting?.CashPaymentsEnabled ?? true,
                transferPaymentsEnabled: systemSetting?.TransferPaymentsEnabled ?? true,
                posPaymentsEnabled: systemSetting?.PosPaymentsEnabled ?? true,
                creditPaymentsEnabled: systemSetting?.CreditPaymentsEnabled ?? true,
                discountsEnabled: systemSetting?.DiscountsEnabled ?? false,
                defaultMaxDiscountPercent: systemSetting?.DefaultMaxDiscountPercent ?? 0m,
                nitLookupProvider: systemSetting?.NitLookupProvider ?? NitLookupProviderType.None);

            if (systemSetting?.CurrencyId.HasValue == true)
            {
                var cur = await db.Currencies.FindAsync([systemSetting.CurrencyId.Value], cancellationToken);
                if (cur != null) companySetting.SelectCurrency(cur);
            }

            if (systemSetting?.FelEnabled == true)
            {
                companySetting.Update(
                    companySetting.BusinessName,
                    companySetting.BusinessNit,
                    companySetting.BusinessAddress,
                    companySetting.BusinessPhone,
                    companySetting.BusinessLogoUrl,
                    companySetting.CurrencyCode,
                    companySetting.CurrencyName,
                    companySetting.CurrencySymbol,
                    companySetting.CurrencyDecimals,
                    companySetting.DecimalSeparator,
                    companySetting.ThousandsSeparator,
                    companySetting.CurrencySymbolPosition,
                    companySetting.TaxPercent,
                    companySetting.ShowTaxOnReceipt,
                    companySetting.ReceiptFooter,
                    companySetting.CashPaymentsEnabled,
                    companySetting.TransferPaymentsEnabled,
                    companySetting.PosPaymentsEnabled,
                    companySetting.CreditPaymentsEnabled,
                    companySetting.DiscountsEnabled,
                    companySetting.DefaultMaxDiscountPercent,
                    companySetting.NitLookupProvider,
                    companySetting.BusinessEmail,
                    companySetting.BusinessLegalName,
                    felEnabled: systemSetting.FelEnabled,
                    felEnvironment: systemSetting.FelEnvironment,
                    defaultFelDocumentType: systemSetting.DefaultFelDocumentType,
                    felTaxPhrases: systemSetting.FelTaxPhrases,
                    infileApiUserProtected: systemSetting.InfileApiUserProtected,
                    infileApiKeyProtected: systemSetting.InfileApiKeyProtected,
                    infileSignatureUserProtected: systemSetting.InfileSignatureUserProtected,
                    infileSignatureKeyProtected: systemSetting.InfileSignatureKeyProtected);
            }

            await db.CompanySettings.AddAsync(companySetting, cancellationToken);
        }

        if (companiesWithoutSettings.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        // 3. Asegurar sucursal predeterminada
        var defaultBranch = await db.Branches.FirstOrDefaultAsync(x => x.CompanyId == defaultCompany.Id, cancellationToken);
        if (defaultBranch is null)
        {
            defaultBranch = new Branch(
                companyId: defaultCompany.Id,
                code: "SUC-01",
                name: "Sucursal Central",
                address: defaultCompany.Address ?? "Central",
                phone: defaultCompany.Phone,
                timeZone: "America/Guatemala",
                establishmentCode: "1");

            await db.Branches.AddAsync(defaultBranch, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 4. Asegurar bodega predeterminada
        var defaultWarehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.BranchId == defaultBranch.Id && x.IsDefault, cancellationToken);
        if (defaultWarehouse is null)
        {
            defaultWarehouse = new Warehouse(
                branchId: defaultBranch.Id,
                code: "BOD-01",
                name: "Bodega Principal",
                description: "Bodega principal de almacenamiento",
                isDefault: true);

            await db.Warehouses.AddAsync(defaultWarehouse, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        // 5. Conciliación de datos históricos (ORG-10)
        // Mapear existencias previas a la bodega predeterminada
        var unassignedStocks = await db.InventoryStocks
            .Where(x => x.WarehouseId == null)
            .ToListAsync(cancellationToken);
        foreach (var stock in unassignedStocks)
        {
            stock.AssignWarehouse(defaultWarehouse.Id);
        }

        var unassignedLots = await db.InventoryExpiryLots.Where(x => x.WarehouseId == null).ToListAsync(cancellationToken);
        foreach (var lot in unassignedLots) lot.AssignWarehouse(defaultWarehouse.Id);

        // Mapear movimientos de inventario previos a la bodega predeterminada
        var unassignedMovements = await db.InventoryMovements
            .Where(x => x.WarehouseId == null)
            .ToListAsync(cancellationToken);
        foreach (var movement in unassignedMovements)
        {
            movement.AssignWarehouse(defaultWarehouse.Id);
        }

        // Mapear sesiones de caja previas a la sucursal predeterminada
        var unassignedCashSessions = await db.CashSessions
            .Where(x => x.BranchId == null)
            .ToListAsync(cancellationToken);
        foreach (var session in unassignedCashSessions)
        {
            session.AssignBranch(defaultBranch.Id);
        }

        // 6. Asignar alcances a usuarios existentes
        var users = await userManager.Users.ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            var hasScope = await db.UserScopeAssignments.AnyAsync(x => x.UserId == user.Id, cancellationToken);
            if (!hasScope)
            {
                var roles = await userManager.GetRolesAsync(user);
                var isSuperAdmin = roles.Contains(SecurityRoleNames.SuperAdministrator, StringComparer.OrdinalIgnoreCase);

                if (isSuperAdmin)
                {
                    db.UserScopeAssignments.Add(new UserScopeAssignment(
                        user.Id,
                        UserScopeLevel.Global,
                        isDefault: true));
                }
                else
                {
                    db.UserScopeAssignments.Add(new UserScopeAssignment(
                        user.Id,
                        UserScopeLevel.Branch,
                        branchId: defaultBranch.Id,
                        isDefault: true));
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
