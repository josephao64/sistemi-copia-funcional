using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Customers;
using SistemiPos.Domain.Finance;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Purchases;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Suppliers;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.PerformanceSeeder;

internal static class PerformanceDataSeeder
{
    private const string MarkerRecordType = "PerformanceData";
    private const string MarkerRecordId = "PERF-SEED-20260913";
    private const string SeederUser = "performance-seeder";
    private static readonly DateTimeOffset EndDate = new(2026, 9, 13, 23, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartDate = new(2025, 9, 15, 0, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<string> SeederTables = new(StringComparer.Ordinal)
    {
        "AuditLogs",
        "Brands",
        "Branches",
        "BusinessGroups",
        "CashMovements",
        "CashSessions",
        "Categories",
        "Companies",
        "Currencies",
        "Customers",
        "InventoryExpiryLots",
        "InventoryMovements",
        "InventoryStocks",
        "PayableAccounts",
        "PayablePayments",
        "Products",
        "ProductSuppliers",
        "PurchaseItems",
        "Purchases",
        "ReceivableAccounts",
        "ReceivablePayments",
        "SaleItems",
        "SalePayments",
        "Sales",
        "Suppliers",
        "SystemSettings",
        "Warehouses"
    };
    private static readonly PropertyInfo IdProperty = typeof(Entity).GetProperty(nameof(Entity.Id))
        ?? throw new InvalidOperationException("No se encontró la propiedad Entity.Id.");

    public static async Task<int> RunAsync(SeederOptions options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var connectionString = Environment.GetEnvironmentVariable(SeederOptions.ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Defina la variable {SeederOptions.ConnectionEnvironmentVariable} con la conexión local de rendimiento.");

        var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        ValidateConnectionTarget(connectionBuilder);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionBuilder.ConnectionString, npgsql => npgsql.CommandTimeout(600))
            .EnableDetailedErrors()
            .Options;

        await using var db = new AppDbContext(dbOptions);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await ValidateConnectedDatabaseAsync(db, cancellationToken);

        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0)
            throw new InvalidOperationException(
                $"La base de rendimiento tiene {pendingMigrations.Length} migración(es) pendiente(s). El seeder no modifica el esquema.");

        var hasPerformanceData = await HasPerformanceDataAsync(db, cancellationToken);
        var populatedUnexpectedTable = await FindPopulatedUnexpectedTableAsync(db, cancellationToken);
        var hasApplicationData = await HasApplicationDataAsync(db, cancellationToken)
            || populatedUnexpectedTable is not null;
        if ((hasPerformanceData || hasApplicationData) && !options.Reset)
            throw new InvalidOperationException(
                "La base ya contiene datos. Use --reset explícitamente para reemplazar únicamente el conjunto de rendimiento.");

        if (options.Reset && (hasPerformanceData || hasApplicationData))
            await ValidateResetScopeAsync(db, hasPerformanceData, populatedUnexpectedTable, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (options.Reset)
        {
            Console.WriteLine("[reset] Limpiando datos de la base exclusiva de rendimiento...");
            await ResetPerformanceDatabaseAsync(db, cancellationToken);
        }

        var random = new Random(SeederOptions.Seed);
        var catalog = await SeedCatalogAsync(db, options, random, cancellationToken);
        var organization = await SeedOrganizationAsync(db, cancellationToken);
        var suppliers = await SeedSuppliersAsync(db, options.Suppliers, cancellationToken);
        var customers = await SeedCustomersAsync(db, options.Customers, cancellationToken);
        await SeedProductSuppliersAsync(db, catalog.OperationalProducts, suppliers, cancellationToken);

        var sales = CreateSaleSeeds(options.Sales, catalog.OperationalProducts, customers, random);
        var purchases = CreatePurchaseSeeds(options.Purchases, catalog.OperationalProducts, suppliers, random);

        var cashSessionCount = await SeedCashSessionsAsync(db, sales, organization.BranchId, cancellationToken);
        await SeedSalesAsync(db, sales, organization.WarehouseId, cancellationToken);
        var receivableCount = await SeedReceivablesAsync(db, sales, cancellationToken);
        await SeedPurchasesAsync(db, purchases, organization, cancellationToken);
        var payableCount = await SeedPayablesAsync(db, purchases, cancellationToken);

        var inventory = await SeedInventoryAsync(
            db,
            catalog.OperationalProducts,
            sales,
            purchases,
            options.InventoryMovements,
            random,
            organization.WarehouseId,
            cancellationToken);

        var marker = Identify(new AuditLog
        {
            Module = "Performance",
            Action = "Seed",
            RecordType = MarkerRecordType,
            RecordId = MarkerRecordId,
            DocumentNumber = $"PERF-{SeederOptions.Seed}",
            UserName = SeederUser,
            OccurredAtUtc = EndDate,
            ChangesJson = JsonSerializer.Serialize(options)
        }, "marker", 1);
        db.AuditLogs.Add(marker);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var result = await ReadResultAsync(db, stopwatch.Elapsed, cancellationToken);
        PrintResult(result, receivableCount, payableCount, cashSessionCount, inventory.ExpiryLots);
        return 0;
    }

    private static void ValidateConnectionTarget(NpgsqlConnectionStringBuilder connection)
    {
        if (!string.Equals(connection.Database, SeederOptions.RequiredDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Base rechazada: '{connection.Database}'. Debe ser exactamente '{SeederOptions.RequiredDatabase}'.");

        var localHosts = new[] { "localhost", "127.0.0.1", "::1" };
        if (!localHosts.Contains(connection.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Host rechazado: '{connection.Host}'. El generador solo permite PostgreSQL local.");
    }

    private static async Task ValidateConnectedDatabaseAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT current_database();";
        var connectedDatabase = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(connectedDatabase, SeederOptions.RequiredDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"La conexión efectiva apunta a '{connectedDatabase}', no a '{SeederOptions.RequiredDatabase}'.");
        Console.WriteLine($"[protección] Base validada: {connectedDatabase}; host local validado.");
    }

    private static async Task<bool> HasPerformanceDataAsync(AppDbContext db, CancellationToken cancellationToken) =>
        await db.AuditLogs.AsNoTracking().AnyAsync(
            x => x.RecordType == MarkerRecordType && x.RecordId == MarkerRecordId,
            cancellationToken)
        || await db.Products.AsNoTracking().AnyAsync(x => x.InternalCode.StartsWith("PERF-"), cancellationToken);

    private static async Task<bool> HasApplicationDataAsync(AppDbContext db, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Customers.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Suppliers.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Sales.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Purchases.AsNoTracking().AnyAsync(cancellationToken)
        || await db.InventoryMovements.AsNoTracking().AnyAsync(cancellationToken)
        || await db.CashSessions.AsNoTracking().AnyAsync(cancellationToken)
        || await db.SystemSettings.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Currencies.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Categories.AsNoTracking().AnyAsync(cancellationToken)
        || await db.Brands.AsNoTracking().AnyAsync(cancellationToken)
        || await db.AuditLogs.AsNoTracking().AnyAsync(cancellationToken);

    private static async Task ValidateResetScopeAsync(
        AppDbContext db,
        bool hasPerformanceData,
        string? populatedUnexpectedTable,
        CancellationToken cancellationToken)
    {
        if (!hasPerformanceData)
            throw new InvalidOperationException(
                "--reset rechazado: la base contiene datos, pero no el marcador del conjunto sintético.");

        var hasUnexpectedSeederTableData =
            await db.Products.AsNoTracking().AnyAsync(x => !x.InternalCode.StartsWith("PERF-P-"), cancellationToken)
            || await db.Customers.AsNoTracking().AnyAsync(x => !x.Name.StartsWith("Cliente rendimiento "), cancellationToken)
            || await db.Suppliers.AsNoTracking().AnyAsync(x => !x.LegalName.StartsWith("Proveedor rendimiento "), cancellationToken)
            || await db.Sales.AsNoTracking().AnyAsync(x => !x.Number.StartsWith("PERF-V-"), cancellationToken)
            || await db.Purchases.AsNoTracking().AnyAsync(x => !x.Number.StartsWith("PERF-C-"), cancellationToken)
            || await db.CashSessions.AsNoTracking().AnyAsync(x => !x.RegisterCode.StartsWith("PERF-CAJA-"), cancellationToken)
            || await db.BusinessGroups.AsNoTracking().AnyAsync(x => !x.Code.StartsWith("PERF-"), cancellationToken)
            || await db.Companies.AsNoTracking().AnyAsync(x => !x.Code.StartsWith("PERF-"), cancellationToken)
            || await db.Branches.AsNoTracking().AnyAsync(x => !x.Code.StartsWith("PERF-"), cancellationToken)
            || await db.Warehouses.AsNoTracking().AnyAsync(x => !x.Code.StartsWith("PERF-"), cancellationToken)
            || await db.Categories.AsNoTracking().AnyAsync(x => !x.Name.StartsWith("Categoría rendimiento "), cancellationToken)
            || await db.Brands.AsNoTracking().AnyAsync(x => !x.Name.StartsWith("Marca rendimiento "), cancellationToken)
            || await db.Currencies.AsNoTracking().AnyAsync(x => x.Code != "GTQ", cancellationToken)
            || await db.Currencies.AsNoTracking().CountAsync(cancellationToken) > 1
            || await db.SystemSettings.AsNoTracking().CountAsync(cancellationToken) > 1
            || await db.AuditLogs.AsNoTracking().AnyAsync(
                x => x.RecordType != MarkerRecordType || x.RecordId != MarkerRecordId,
                cancellationToken);
        if (hasUnexpectedSeederTableData)
            throw new InvalidOperationException(
                "--reset rechazado: se encontraron registros que no pertenecen al conjunto sintético.");

        if (populatedUnexpectedTable is not null)
            throw new InvalidOperationException(
                $"--reset rechazado: la tabla '{populatedUnexpectedTable}' contiene datos ajenos al generador.");
    }

    private static async Task<string?> FindPopulatedUnexpectedTableAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var tableNames = new List<string>();
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                tableNames.Add(reader.GetString(0));
        }

        using var commandBuilder = new NpgsqlCommandBuilder();
        foreach (var tableName in tableNames)
        {
            if (SeederTables.Contains(tableName)
                || tableName is "__EFMigrationsHistory" or "PurchasePresentations")
                continue;

            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT EXISTS (SELECT 1 FROM {commandBuilder.QuoteIdentifier(tableName)} LIMIT 1);";
            if (Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken)))
                return tableName;
        }

        return null;
    }

    private static async Task ResetPerformanceDatabaseAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
            TRUNCATE TABLE
                "AuditLogs", "Brands", "Branches", "BusinessGroups", "CashMovements",
                "CashSessions", "Categories", "Companies", "Currencies", "Customers",
                "InventoryExpiryLots", "InventoryMovements",
                "InventoryStocks", "PayableAccounts", "PayablePayments", "Products",
                "ProductSuppliers", "PurchaseItems", "Purchases", "ReceivableAccounts",
                "ReceivablePayments", "SaleItems", "SalePayments", "Sales", "Suppliers",
                "SystemSettings", "Warehouses"
            RESTART IDENTITY CASCADE;
            """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static async Task<OrganizationSeed> SeedOrganizationAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[estructura] Grupo, empresa, sucursales y bodegas...");
        var group = Stamp(Identify(new BusinessGroup(
            "PERF-GRP", "Grupo de rendimiento", "Grupo de rendimiento, S.A.",
            "PERF-100000-1", "Estructura sintética para pruebas de volumen."),
            "business-group", 1), StartDate);
        var company = Stamp(Identify(new Company(
            group.Id, "PERF-EMP", "Empresa de rendimiento", "Empresa de rendimiento, S.A.",
            "PERF-200000-2", "rendimiento@sistemi.local", "2200-0000",
            "Ciudad de Guatemala", "Régimen General"), "company", 1), StartDate);

        var branches = Enumerable.Range(1, 4).Select(index => Stamp(Identify(new Branch(
            company.Id, $"PERF-SUC-{index:00}", $"Sucursal rendimiento {index:00}",
            $"Zona {index}, Guatemala", $"2200-{index:0000}", "America/Guatemala", index.ToString()),
            "branch", index), StartDate)).ToList();
        var warehouses = branches.SelectMany((branch, branchIndex) => Enumerable.Range(1, 2)
            .Select(warehouseIndex => Stamp(Identify(new Warehouse(
                branch.Id,
                $"PERF-BOD-{branchIndex + 1:00}-{warehouseIndex:00}",
                warehouseIndex == 1 ? $"Bodega principal {branchIndex + 1:00}" : $"Bodega reserva {branchIndex + 1:00}",
                "Bodega sintética para pruebas de rendimiento.",
                isDefault: warehouseIndex == 1),
                "warehouse", branchIndex * 2 + warehouseIndex), StartDate))).ToList();

        db.AddRange(group, company);
        db.AddRange(branches);
        db.AddRange(warehouses);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new OrganizationSeed(branches[0].Id, warehouses[0].Id);
    }

    private static async Task<CatalogSeed> SeedCatalogAsync(
        AppDbContext db,
        SeederOptions options,
        Random random,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[1/9] Catálogos, moneda GTQ y productos...");
        var categories = Enumerable.Range(1, 50)
            .Select(index => Stamp(Identify(
                new Category($"Categoría rendimiento {index:00}", "Datos sintéticos para pruebas de rendimiento."),
                "category",
                index), StartDate))
            .ToList();
        var brands = Enumerable.Range(1, 30)
            .Select(index => Stamp(Identify(new Brand($"Marca rendimiento {index:00}"), "brand", index), StartDate))
            .ToList();

        var currency = Stamp(Identify(
            new CurrencyDefinition("GTQ", "Quetzal", "Q", 2, ".", ",", CurrencySymbolPosition.Before),
            "currency",
            1), StartDate);
        var setting = Stamp(Identify(SystemSetting.CreateDefault(), "setting", 1), StartDate);
        setting.SelectCurrency(currency);

        await InsertBatchesAsync(db, categories, 500, "categorías", cancellationToken);
        await InsertBatchesAsync(db, brands, 500, "marcas", cancellationToken);
        db.AddRange(currency, setting);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var parentCount = options.Products / 10;
        var childCount = Math.Min(parentCount * 2, options.Products - parentCount);
        var standaloneCount = options.Products - parentCount - childCount;
        var parents = new List<Product>(parentCount);
        var products = new List<Product>(options.Products);
        var operational = new List<ProductSeed>(options.Products - parentCount);
        var globalIndex = 0;
        var operationalIndex = 0;

        for (var index = 0; index < parentCount; index++)
        {
            globalIndex++;
            var product = CreateProduct(globalIndex, categories, brands, random, false);
            product.SetManagesVariants(true);
            parents.Add(product);
            products.Add(product);
        }

        for (var index = 0; index < standaloneCount; index++)
        {
            globalIndex++;
            operationalIndex++;
            var product = CreateProduct(globalIndex, categories, brands, random, operationalIndex % 5 == 0);
            products.Add(product);
            operational.Add(ToSeed(product, operationalIndex));
        }

        for (var index = 0; index < childCount; index++)
        {
            globalIndex++;
            operationalIndex++;
            var parent = parents[index % parents.Count];
            var product = CreateProduct(globalIndex, categories, brands, random, operationalIndex % 5 == 0);
            product.SetVariant(parent.Id, $"Variante {(index % 12) + 1:00}");
            products.Add(product);
            operational.Add(ToSeed(product, operationalIndex));
        }

        await InsertBatchesAsync(db, products, 500, "productos/variantes", cancellationToken);
        return new CatalogSeed(operational);
    }

    private static Product CreateProduct(
        int index,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Brand> brands,
        Random random,
        bool managesExpiry)
    {
        var cost = decimal.Round(2m + (decimal)random.NextDouble() * 198m, 2);
        var price = decimal.Round(cost * (1.18m + (decimal)random.NextDouble() * .67m), 2);
        var product = Identify(new Product(
            $"PERF-P-{index:000000}",
            $"740000{index:000000}",
            $"Producto rendimiento {index:000000}",
            "Producto sintético determinístico para pruebas de carga.",
            categories[(index - 1) % categories.Count].Id,
            brands[(index - 1) % brands.Count].Name,
            index % 8 == 0 ? "Libra" : "Unidad",
            cost,
            decimal.Round(cost * .95m, 2),
            price,
            10 + index % 21,
            index % 9 != 0,
            index % 7 == 0 ? 60 : null), "product", index);
        product.ConfigureExpiry(managesExpiry, managesExpiry ? 30 : null);
        return Stamp(product, StartDate.AddDays(index % 365));
    }

    private static ProductSeed ToSeed(Product product, int index) =>
        new(product.Id, product.InternalCode, product.DisplayName, product.LastCost, product.SalePrice,
            product.MinimumStock, product.ManagesExpiry, index);

    private static async Task<List<SupplierSeed>> SeedSuppliersAsync(
        AppDbContext db,
        int count,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[2/9] Proveedores...");
        var seeds = new List<SupplierSeed>(count);
        var entities = new List<Supplier>(count);
        for (var index = 1; index <= count; index++)
        {
            var entity = Identify(new Supplier(
                $"Proveedor rendimiento {index:0000}, S.A.",
                $"90{index:000000}-K",
                $"Proveedor PERF {index:0000}",
                $"+502 24{index % 10000:0000}000",
                $"proveedor{index:0000}@performance.local",
                $"Zona {(index % 20) + 1}, Guatemala",
                $"Contacto rendimiento {index:0000}"), "supplier", index);
            var credit = index % 2 == 0;
            entity.Update(entity.LegalName, entity.Nit, entity.TradeName, entity.Phone, entity.Email,
                entity.Address, entity.ContactName, true, credit ? 30 : null,
                credit ? PurchaseTermsType.Credit : PurchaseTermsType.Cash);
            Stamp(entity, StartDate.AddDays(index % 365));
            entities.Add(entity);
            seeds.Add(new SupplierSeed(entity.Id, entity.LegalName, entity.Nit, index));
        }
        await InsertBatchesAsync(db, entities, 500, "proveedores", cancellationToken);
        return seeds;
    }

    private static async Task<List<CustomerSeed>> SeedCustomersAsync(
        AppDbContext db,
        int count,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[3/9] Clientes...");
        var seeds = new List<CustomerSeed>(count);
        var entities = new List<Customer>(count);
        for (var index = 1; index <= count; index++)
        {
            var entity = Stamp(Identify(new Customer(
                $"Cliente rendimiento {index:00000}",
                $"80{index:000000}-K",
                $"+502 55{index % 1000000:000000}",
                $"cliente{index:00000}@performance.local",
                $"Dirección sintética {index:00000}, Guatemala",
                30,
                index % 4), "customer", index), StartDate.AddDays(index % 365));
            entities.Add(entity);
            seeds.Add(new CustomerSeed(entity.Id, entity.Name, entity.Nit, entity.Phone, entity.Address, index));
        }
        await InsertBatchesAsync(db, entities, 500, "clientes", cancellationToken);
        return seeds;
    }

    private static async Task SeedProductSuppliersAsync(
        AppDbContext db,
        IReadOnlyList<ProductSeed> products,
        IReadOnlyList<SupplierSeed> suppliers,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[4/9] Relaciones producto-proveedor...");
        var entities = new List<ProductSupplier>(products.Count + products.Count / 4);
        var relationIndex = 0;
        foreach (var product in products)
        {
            var primary = suppliers[product.Index % suppliers.Count];
            var relation = Identify(new ProductSupplier(product.Id, primary.Id, product.Name,
                $"PS-{product.Index:000000}-1", SupplierPackageType.Unit, 1,
                product.Index % 2 == 0 ? PurchaseTermsType.Credit : PurchaseTermsType.Cash,
                product.Index % 2 == 0 ? 30 : null), "product-supplier", ++relationIndex);
            relation.RegisterPurchase(product.Cost, EndDate.AddDays(-(product.Index % 365)),
                product.Index % 2 == 0 ? PurchaseTermsType.Credit : PurchaseTermsType.Cash,
                product.Index % 2 == 0 ? 30 : null);
            entities.Add(Stamp(relation, StartDate));

            if (suppliers.Count > 1 && product.Index % 4 == 0)
            {
                var secondary = suppliers[(product.Index + 17) % suppliers.Count];
                if (secondary.Id != primary.Id)
                {
                    var second = Identify(new ProductSupplier(product.Id, secondary.Id, product.Name,
                        $"PS-{product.Index:000000}-2", SupplierPackageType.Unit, 1,
                        PurchaseTermsType.Cash, null), "product-supplier", ++relationIndex);
                    second.RegisterPurchase(decimal.Round(product.Cost * 1.03m, 2), EndDate.AddDays(-(product.Index % 180)),
                        PurchaseTermsType.Cash, null);
                    entities.Add(Stamp(second, StartDate));
                }
            }
        }
        await InsertBatchesAsync(db, entities, 500, "relaciones producto-proveedor", cancellationToken);
    }

    private static List<SaleSeed> CreateSaleSeeds(
        int count,
        IReadOnlyList<ProductSeed> products,
        IReadOnlyList<CustomerSeed> customers,
        Random random)
    {
        var creditTarget = Math.Min(count, Math.Clamp((int)Math.Round(count * .15), Math.Min(500, count), Math.Min(1_000, count)));
        var sales = new List<SaleSeed>(count);
        for (var index = 1; index <= count; index++)
        {
            var credit = index <= creditTarget;
            var date = CreateTransactionDate(credit, index - 1, random);
            var sessionDay = (date - StartDate).Days;
            var sessionId = DeterministicGuid("cash-session", sessionDay + 1);
            CustomerSeed? customer = credit || random.NextDouble() < .7
                ? customers[random.Next(customers.Count)]
                : null;
            var used = new HashSet<Guid>();
            var lines = new List<TransactionLineSeed>(5);
            while (lines.Count < 5)
            {
                var product = products[random.Next(products.Count)];
                if (!used.Add(product.Id)) continue;
                var quantity = random.Next(1, 6);
                lines.Add(new TransactionLineSeed(product, quantity, product.Price));
            }
            var total = decimal.Round(lines.Sum(x => x.Quantity * x.UnitValue), 2);
            sales.Add(new SaleSeed(
                DeterministicGuid("sale", index),
                index,
                $"PERF-V-{index:0000000}",
                sessionId,
                customer,
                date,
                credit,
                lines,
                total));
        }
        return sales;
    }

    private static List<PurchaseSeed> CreatePurchaseSeeds(
        int count,
        IReadOnlyList<ProductSeed> products,
        IReadOnlyList<SupplierSeed> suppliers,
        Random random)
    {
        var productsBySupplier = suppliers.ToDictionary(
            supplier => supplier.Id,
            _ => new List<PurchaseProductSeed>());
        foreach (var product in products)
        {
            var primary = suppliers[product.Index % suppliers.Count];
            productsBySupplier[primary.Id].Add(new PurchaseProductSeed(
                product,
                $"PS-{product.Index:000000}-1"));

            if (suppliers.Count > 1 && product.Index % 4 == 0)
            {
                var secondary = suppliers[(product.Index + 17) % suppliers.Count];
                if (secondary.Id != primary.Id)
                    productsBySupplier[secondary.Id].Add(new PurchaseProductSeed(
                        product,
                        $"PS-{product.Index:000000}-2"));
            }
        }

        var eligibleSuppliers = suppliers
            .Where(supplier => productsBySupplier[supplier.Id].Select(x => x.Product.Id).Distinct().Count() >= 5)
            .ToArray();
        if (eligibleSuppliers.Length == 0)
            throw new InvalidOperationException(
                "La combinación de productos y proveedores no permite crear compras con cinco productos relacionados.");

        var creditTarget = Math.Min(count, Math.Clamp((int)Math.Round(count * .4), Math.Min(500, count), Math.Min(1_000, count)));
        var purchases = new List<PurchaseSeed>(count);
        for (var index = 1; index <= count; index++)
        {
            var credit = index <= creditTarget;
            var date = CreateTransactionDate(credit, index - 1, random);
            var supplier = eligibleSuppliers[random.Next(eligibleSuppliers.Length)];
            var availableProducts = productsBySupplier[supplier.Id];
            var used = new HashSet<Guid>();
            var lines = new List<PurchaseLineSeed>(5);
            while (lines.Count < 5)
            {
                var candidate = availableProducts[random.Next(availableProducts.Count)];
                if (!used.Add(candidate.Product.Id)) continue;
                var quantity = random.Next(6, 31);
                lines.Add(new PurchaseLineSeed(
                    candidate.Product,
                    quantity,
                    candidate.Product.Cost,
                    candidate.SupplierProductCode));
            }
            var total = decimal.Round(lines.Sum(x => x.Quantity * x.UnitValue), 2);
            purchases.Add(new PurchaseSeed(
                DeterministicGuid("purchase", index),
                index,
                $"PERF-C-{index:0000000}",
                $"PERF-FAC-{index:0000000}",
                supplier,
                date,
                credit,
                lines,
                total));
        }
        return purchases;
    }

    private static DateTimeOffset CreateTransactionDate(bool credit, int zeroBasedIndex, Random random)
    {
        var day = credit
            ? zeroBasedIndex % 4 == 3
                ? random.Next(335, 365)
                : random.Next(0, 320)
            : random.Next(0, 365);
        return StartDate.AddDays(day).AddHours(8).AddMinutes(random.Next(0, 540));
    }

    private static async Task<int> SeedCashSessionsAsync(
        AppDbContext db,
        IReadOnlyList<SaleSeed> sales,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[5/9] Sesiones históricas de caja...");
        const int sessionCount = 365;
        var salesBySession = sales.Where(x => !x.IsCredit).GroupBy(x => x.CashSessionId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var sessions = new List<CashSession>(sessionCount);
        var movementIndex = 0;
        for (var day = 0; day < sessionCount; day++)
        {
            var id = DeterministicGuid("cash-session", day + 1);
            var openedAt = StartDate.AddDays(day).AddHours(8);
            var cashier = $"perf.cajero{day % 12 + 1:00}";
            var session = Identify(new CashSession($"PERF-CAJA-{day % 8 + 1:00}", cashier, openedAt, 500m,
                branchId: branchId), id);
            Stamp(session, openedAt);
            if (salesBySession.TryGetValue(id, out var daySales))
            {
                foreach (var sale in daySales)
                {
                    var movement = Identify(new CashMovement(CashMovementType.SaleCash, sale.Total,
                        $"Venta sintética {sale.Number}", cashier, sale.Date, null,
                        nameof(Sale), sale.Id, sale.Number), "cash-movement", ++movementIndex);
                    movement.CashSessionId = session.Id;
                    session.AddMovement(Stamp(movement, sale.Date));
                }
            }
            session.Close(openedAt.AddHours(10), session.CalculateExpectedCash(), null,
                "Cierre sintético de rendimiento.", SeederUser);
            sessions.Add(session);
        }
        await InsertBatchesAsync(db, sessions, 25, "sesiones de caja", cancellationToken);
        return sessionCount;
    }

    private static async Task SeedSalesAsync(
        AppDbContext db,
        IReadOnlyList<SaleSeed> sales,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[6/9] Ventas y líneas de venta...");
        var batch = new List<Sale>(100);
        foreach (var seed in sales)
        {
            var lines = seed.Lines.Select((line, position) => Identify(new SaleItem(
                line.Product.Id,
                line.Product.Name,
                line.Quantity,
                line.UnitValue,
                0,
                12,
                false,
                line.Product.Cost), "sale-item", (seed.Index - 1) * 5 + position + 1)).ToList();
            var payment = seed.IsCredit
                ? SalePayment.CreateCredit(seed.Total, $"perf.cajero{seed.Index % 12 + 1:00}")
                : SalePayment.CreateCash(seed.Total, seed.Total + seed.Index % 4 * 10m, $"perf.cajero{seed.Index % 12 + 1:00}");
            Identify(payment, "sale-payment", seed.Index);
            var customer = seed.Customer;
            var sale = Identify(new Sale(
                seed.Number,
                seed.CashSessionId,
                customer?.Id,
                seed.Date,
                $"perf.cajero{seed.Index % 12 + 1:00}",
                lines,
                payment,
                "GTQ",
                "Quetzal",
                "Q",
                2,
                ".",
                ",",
                CurrencySymbolPosition.Before.ToString(),
                customer?.Nit,
                customer?.Name,
                customer?.Address,
                customer?.Phone,
                "Datos de rendimiento",
                warehouseId: warehouseId), seed.Id);
            batch.Add(Stamp(sale, seed.Date));
            if (batch.Count == 100)
                await FlushBatchAsync(db, batch, sales.Count, "ventas", cancellationToken);
        }
        await FlushBatchAsync(db, batch, sales.Count, "ventas", cancellationToken);
    }

    private static async Task<int> SeedReceivablesAsync(
        AppDbContext db,
        IReadOnlyList<SaleSeed> sales,
        CancellationToken cancellationToken)
    {
        var creditSales = sales.Where(x => x.IsCredit).ToList();
        var batch = new List<ReceivableAccount>(250);
        for (var index = 0; index < creditSales.Count; index++)
        {
            var sale = creditSales[index];
            var account = Identify(new ReceivableAccount(sale.Id, sale.Customer!.Id, sale.Number,
                sale.Date, sale.Date.AddDays(30), sale.Total, 30, sale.Index % 4), "receivable", index + 1);
            Stamp(account, sale.Date);
            if (index % 4 == 1 || index % 4 == 2)
            {
                var amount = index % 4 == 2 ? sale.Total : decimal.Round(sale.Total * .4m, 2);
                var payment = Identify(new ReceivablePayment(sale.Date.AddDays(10), amount,
                    FinancialPaymentMethod.Transfer, $"PERF-ABONO-{index + 1:000000}", SeederUser, null, null),
                    "receivable-payment", index + 1);
                account.AddPayment(Stamp(payment, sale.Date.AddDays(10)));
            }
            batch.Add(account);
            if (batch.Count == 250)
                await FlushBatchAsync(db, batch, creditSales.Count, "cuentas por cobrar", cancellationToken);
        }
        await FlushBatchAsync(db, batch, creditSales.Count, "cuentas por cobrar", cancellationToken);
        return creditSales.Count;
    }

    private static async Task SeedPurchasesAsync(
        AppDbContext db,
        IReadOnlyList<PurchaseSeed> purchases,
        OrganizationSeed organization,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[7/9] Compras y líneas de compra...");
        var batch = new List<Purchase>(100);
        foreach (var seed in purchases)
        {
            var lines = seed.Lines.Select((line, position) => Identify(new PurchaseItem(
                line.Product.Id,
                line.Quantity,
                line.UnitValue,
                SupplierPackageType.Unit,
                line.Quantity,
                1,
                line.UnitValue,
                line.Product.Name,
                line.SupplierProductCode), "purchase-item", (seed.Index - 1) * 5 + position + 1)).ToList();
            var purchase = Identify(new Purchase(seed.Number, seed.Supplier.Id, seed.Date, SeederUser, lines,
                seed.IsCredit ? PurchaseTermsType.Credit : PurchaseTermsType.Cash,
                seed.IsCredit ? seed.Date.AddDays(30) : null,
                null,
                seed.InvoiceNumber,
                "Compra sintética para rendimiento.",
                seed.Supplier.Name,
                seed.Supplier.Nit,
                branchId: organization.BranchId,
                warehouseId: organization.WarehouseId), seed.Id);
            purchase.Confirm(seed.Date.AddMinutes(30), SeederUser);
            batch.Add(Stamp(purchase, seed.Date));
            if (batch.Count == 100)
                await FlushBatchAsync(db, batch, purchases.Count, "compras", cancellationToken);
        }
        await FlushBatchAsync(db, batch, purchases.Count, "compras", cancellationToken);
    }

    private static async Task<int> SeedPayablesAsync(
        AppDbContext db,
        IReadOnlyList<PurchaseSeed> purchases,
        CancellationToken cancellationToken)
    {
        var creditPurchases = purchases.Where(x => x.IsCredit).ToList();
        var batch = new List<PayableAccount>(250);
        for (var index = 0; index < creditPurchases.Count; index++)
        {
            var purchase = creditPurchases[index];
            var account = Identify(new PayableAccount(purchase.Id, purchase.Supplier.Id, purchase.InvoiceNumber,
                purchase.Date, purchase.Date.AddDays(30), purchase.Total), "payable", index + 1);
            Stamp(account, purchase.Date);
            if (index % 4 == 1 || index % 4 == 2)
            {
                var amount = index % 4 == 2 ? purchase.Total : decimal.Round(purchase.Total * .4m, 2);
                var payment = Identify(new PayablePayment(purchase.Date.AddDays(10), amount,
                    FinancialPaymentMethod.Transfer, MoneySourceType.Other, null,
                    $"PERF-PAGO-{index + 1:000000}", SeederUser, null), "payable-payment", index + 1);
                account.AddPayment(Stamp(payment, purchase.Date.AddDays(10)));
            }
            batch.Add(account);
            if (batch.Count == 250)
                await FlushBatchAsync(db, batch, creditPurchases.Count, "cuentas por pagar", cancellationToken);
        }
        await FlushBatchAsync(db, batch, creditPurchases.Count, "cuentas por pagar", cancellationToken);
        return creditPurchases.Count;
    }

    private static async Task<InventorySeedResult> SeedInventoryAsync(
        AppDbContext db,
        IReadOnlyList<ProductSeed> products,
        IReadOnlyList<SaleSeed> sales,
        IReadOnlyList<PurchaseSeed> purchases,
        int requestedMovements,
        Random random,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("[8/9] Existencias, lotes y movimientos de inventario...");
        var events = products.ToDictionary(x => x.Id, _ => new List<MovementSeed>());
        foreach (var sale in sales)
            foreach (var line in sale.Lines)
                events[line.Product.Id].Add(new MovementSeed(InventoryMovementType.SaleExit, line.Quantity,
                    sale.Date, sale.Number, null));
        foreach (var purchase in purchases)
            foreach (var line in purchase.Lines)
                events[line.Product.Id].Add(new MovementSeed(InventoryMovementType.PurchaseEntry, line.Quantity,
                    purchase.Date, purchase.Number, purchase.Id));

        var baseMovements = sales.Sum(x => x.Lines.Count) + purchases.Sum(x => x.Lines.Count) + products.Count;
        if (requestedMovements < baseMovements)
            throw new InvalidOperationException(
                $"--inventory-movements debe ser al menos {baseMovements} para conservar movimientos de cada línea y existencia final.");

        var extraMovements = requestedMovements - baseMovements;
        for (var index = 0; index < extraMovements; index++)
        {
            var product = products[random.Next(products.Count)];
            var type = index % 2 == 0 ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment;
            events[product.Id].Add(new MovementSeed(type, random.Next(1, 11),
                StartDate.AddDays(random.Next(0, 364)).AddMinutes(random.Next(0, 1_080)),
                $"PERF-AJ-{index + 1:000000}", null));
        }

        var movements = new List<InventoryMovement>(requestedMovements);
        var stocks = new List<InventoryStock>(products.Count);
        var lots = new List<InventoryExpiryLot>();
        var movementIndex = 0;
        var lotIndex = 0;
        foreach (var product in products)
        {
            decimal current = 1_000 + product.Index % 100;
            foreach (var movement in events[product.Id].OrderBy(x => x.Date))
            {
                var previous = current;
                current = movement.Type is InventoryMovementType.PurchaseEntry
                    or InventoryMovementType.PositiveAdjustment
                    or InventoryMovementType.ReturnEntry
                    or InventoryMovementType.CancellationReversal
                    ? current + movement.Quantity
                    : current - movement.Quantity;
                if (current < 0)
                    throw new InvalidOperationException($"Existencia negativa al generar {product.InternalCode}.");
                movements.Add(CreateMovement(product.Id, movement, previous, current, ++movementIndex, warehouseId));
            }

            var target = product.Index % 10 switch
            {
                0 => 0m,
                1 or 2 => 1 + product.Index % Math.Max(1, (int)product.MinimumStock),
                _ => 25 + product.Index % 476
            };
            if (target == current) target = target == 0 ? 1 : target - 1;
            var finalType = target > current ? InventoryMovementType.PositiveAdjustment : InventoryMovementType.NegativeAdjustment;
            var finalSeed = new MovementSeed(finalType, Math.Abs(target - current), EndDate,
                $"PERF-STOCK-{product.Index:000000}", null);
            movements.Add(CreateMovement(product.Id, finalSeed, current, target, ++movementIndex, warehouseId));

            var stock = Identify(new InventoryStock(product.Id, warehouseId), "stock", product.Index);
            stock.ReplaceForSystem(target);
            stocks.Add(Stamp(stock, EndDate));

            if (product.ManagesExpiry && target > 0)
            {
                var firstQuantity = target >= 2 ? decimal.Floor(target / 2) : target;
                var secondQuantity = target - firstQuantity;
                lots.Add(Identify(new InventoryExpiryLot(product.Id,
                    DateOnly.FromDateTime(EndDate.UtcDateTime).AddDays(product.Index % 4 == 0 ? -15 : 20 + product.Index % 60),
                    firstQuantity,
                    $"PERF-LOTE-{product.Index:000000}-1",
                    EndDate.AddDays(-(30 + product.Index % 120)),
                    warehouseId,
                    lotNumber: $"PERF-LOTE-{product.Index:000000}-1"), "expiry-lot", ++lotIndex));
                if (secondQuantity > 0)
                    lots.Add(Identify(new InventoryExpiryLot(product.Id,
                        DateOnly.FromDateTime(EndDate.UtcDateTime).AddDays(90 + product.Index % 180),
                        secondQuantity,
                        $"PERF-LOTE-{product.Index:000000}-2",
                        EndDate.AddDays(-(5 + product.Index % 30)),
                        warehouseId,
                        lotNumber: $"PERF-LOTE-{product.Index:000000}-2"), "expiry-lot", ++lotIndex));
            }
        }

        await InsertBatchesAsync(db, stocks, 500, "existencias", cancellationToken);
        await InsertBatchesAsync(db, lots, 500, "lotes", cancellationToken);
        await InsertInventoryMovementsAsync(db, movements, warehouseId, cancellationToken);
        return new InventorySeedResult(lots.Count);
    }

    private static InventoryMovement CreateMovement(
        Guid productId,
        MovementSeed seed,
        decimal previous,
        decimal current,
        int index,
        Guid warehouseId) =>
        Stamp(Identify(new InventoryMovement(productId, seed.Type, seed.Quantity, previous, current,
            seed.Date, SeederUser, seed.SourceDocument, "Dato sintético de rendimiento.",
            purchaseId: seed.PurchaseId, warehouseId: warehouseId), "inventory-movement", index), seed.Date);

    private static async Task InsertInventoryMovementsAsync(
        AppDbContext db,
        IReadOnlyList<InventoryMovement> movements,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY "InventoryMovements" (
                "Id", "ProductId", "WarehouseId", "Type", "Quantity", "PreviousStock", "NewStock",
                "OccurredAtUtc", "UserName", "SourceDocument", "Note", "PhysicalInventoryCountId",
                "PhysicalInventoryCountItemId", "PurchaseOrderId", "PurchaseId", "TransferId",
                "TransferItemId", "UnitCost", "CreatedAtUtc", "CreatedBy", "ModifiedAtUtc", "ModifiedBy")
            FROM STDIN (FORMAT BINARY)
            """, cancellationToken);

        for (var index = 0; index < movements.Count; index++)
        {
            var movement = movements[index];
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(movement.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(movement.ProductId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(warehouseId, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync((int)movement.Type, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(movement.Quantity, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(movement.PreviousStock, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(movement.NewStock, NpgsqlDbType.Numeric, cancellationToken);
            await writer.WriteAsync(movement.OccurredAtUtc, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(movement.UserName, NpgsqlDbType.Varchar, cancellationToken);
            if (movement.SourceDocument is null) await writer.WriteNullAsync(cancellationToken);
            else await writer.WriteAsync(movement.SourceDocument, NpgsqlDbType.Varchar, cancellationToken);
            if (movement.Note is null) await writer.WriteNullAsync(cancellationToken);
            else await writer.WriteAsync(movement.Note, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            if (movement.PurchaseId.HasValue)
                await writer.WriteAsync(movement.PurchaseId.Value, NpgsqlDbType.Uuid, cancellationToken);
            else
                await writer.WriteNullAsync(cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            await writer.WriteAsync(movement.CreatedAtUtc, NpgsqlDbType.TimestampTz, cancellationToken);
            await writer.WriteAsync(SeederUser, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteNullAsync(cancellationToken);
            await writer.WriteNullAsync(cancellationToken);

            if ((index + 1) % 10_000 == 0 || index + 1 == movements.Count)
                Console.WriteLine($"      movimientos de inventario: {index + 1:N0}/{movements.Count:N0}");
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task InsertBatchesAsync<T>(
        AppDbContext db,
        IReadOnlyList<T> entities,
        int batchSize,
        string label,
        CancellationToken cancellationToken) where T : class
    {
        var processed = 0;
        while (processed < entities.Count)
        {
            var batch = entities.Skip(processed).Take(batchSize).ToArray();
            db.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            processed += batch.Length;
            if (processed == entities.Count || processed % Math.Max(batchSize, 1_000) == 0)
                Console.WriteLine($"      {label}: {processed:N0}/{entities.Count:N0}");
        }
    }

    private static async Task FlushBatchAsync<T>(
        AppDbContext db,
        List<T> batch,
        int total,
        string label,
        CancellationToken cancellationToken) where T : class
    {
        if (batch.Count == 0) return;
        db.AddRange(batch);
        await db.SaveChangesAsync(cancellationToken);
        var processed = await db.Set<T>().CountAsync(cancellationToken);
        db.ChangeTracker.Clear();
        batch.Clear();
        if (processed == total || processed % 1_000 == 0)
            Console.WriteLine($"      {label}: {processed:N0}/{total:N0}");
    }

    private static async Task<SeederResult> ReadResultAsync(
        AppDbContext db,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        var result = new SeederResult(
            await db.Products.CountAsync(cancellationToken),
            await db.Customers.CountAsync(cancellationToken),
            await db.Suppliers.CountAsync(cancellationToken),
            await db.Sales.CountAsync(cancellationToken),
            await db.SaleItems.CountAsync(cancellationToken),
            await db.Purchases.CountAsync(cancellationToken),
            await db.PurchaseItems.CountAsync(cancellationToken),
            await db.InventoryMovements.CountAsync(cancellationToken),
            await db.ReceivableAccounts.CountAsync(cancellationToken),
            await db.PayableAccounts.CountAsync(cancellationToken),
            await db.CashSessions.CountAsync(cancellationToken),
            elapsed,
            0);

        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_database_size(current_database());";
        var databaseBytes = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return result with { DatabaseBytes = databaseBytes };
    }

    private static void PrintResult(
        SeederResult result,
        int receivables,
        int payables,
        int cashSessions,
        int expiryLots)
    {
        Console.WriteLine("[9/9] Generación completada.");
        Console.WriteLine($"RESULT Database={SeederOptions.RequiredDatabase}");
        Console.WriteLine($"RESULT Products={result.Products}");
        Console.WriteLine($"RESULT Customers={result.Customers}");
        Console.WriteLine($"RESULT Suppliers={result.Suppliers}");
        Console.WriteLine($"RESULT Sales={result.Sales}");
        Console.WriteLine($"RESULT SaleItems={result.SaleItems}");
        Console.WriteLine($"RESULT Purchases={result.Purchases}");
        Console.WriteLine($"RESULT PurchaseItems={result.PurchaseItems}");
        Console.WriteLine($"RESULT InventoryMovements={result.InventoryMovements}");
        Console.WriteLine($"RESULT Receivables={receivables}");
        Console.WriteLine($"RESULT Payables={payables}");
        Console.WriteLine($"RESULT CashSessions={cashSessions}");
        Console.WriteLine($"RESULT ExpiryLots={expiryLots}");
        Console.WriteLine($"RESULT ElapsedSeconds={result.Elapsed.TotalSeconds:F2}");
        Console.WriteLine($"RESULT DatabaseBytes={result.DatabaseBytes}");
    }

    private static T Identify<T>(T entity, string scope, int index) where T : Entity =>
        Identify(entity, DeterministicGuid(scope, index));

    private static T Identify<T>(T entity, Guid id) where T : Entity
    {
        IdProperty.SetValue(entity, id);
        return entity;
    }

    private static T Stamp<T>(T entity, DateTimeOffset at) where T : AuditableEntity
    {
        entity.CreatedAtUtc = at;
        entity.CreatedBy = SeederUser;
        return entity;
    }

    private static Guid DeterministicGuid(string scope, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{SeederOptions.Seed}:{scope}:{index}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record CatalogSeed(IReadOnlyList<ProductSeed> OperationalProducts);
    private sealed record OrganizationSeed(Guid BranchId, Guid WarehouseId);
    private sealed record ProductSeed(Guid Id, string InternalCode, string Name, decimal Cost,
        decimal Price, decimal MinimumStock, bool ManagesExpiry, int Index);
    private sealed record CustomerSeed(Guid Id, string Name, string? Nit, string? Phone, string? Address, int Index);
    private sealed record SupplierSeed(Guid Id, string Name, string Nit, int Index);
    private sealed record TransactionLineSeed(ProductSeed Product, decimal Quantity, decimal UnitValue);
    private sealed record PurchaseProductSeed(ProductSeed Product, string SupplierProductCode);
    private sealed record PurchaseLineSeed(ProductSeed Product, decimal Quantity, decimal UnitValue,
        string SupplierProductCode);
    private sealed record SaleSeed(Guid Id, int Index, string Number, Guid CashSessionId, CustomerSeed? Customer,
        DateTimeOffset Date, bool IsCredit, IReadOnlyList<TransactionLineSeed> Lines, decimal Total);
    private sealed record PurchaseSeed(Guid Id, int Index, string Number, string InvoiceNumber, SupplierSeed Supplier,
        DateTimeOffset Date, bool IsCredit, IReadOnlyList<PurchaseLineSeed> Lines, decimal Total);
    private sealed record MovementSeed(InventoryMovementType Type, decimal Quantity, DateTimeOffset Date,
        string SourceDocument, Guid? PurchaseId);
    private sealed record InventorySeedResult(int ExpiryLots);
    private sealed record SeederResult(int Products, int Customers, int Suppliers, int Sales, int SaleItems,
        int Purchases, int PurchaseItems, int InventoryMovements, int Receivables, int Payables,
        int CashSessions, TimeSpan Elapsed, long DatabaseBytes);
}
