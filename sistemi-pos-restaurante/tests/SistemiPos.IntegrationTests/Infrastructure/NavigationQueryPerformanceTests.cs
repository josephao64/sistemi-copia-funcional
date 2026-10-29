using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Configuration;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Infrastructure.Catalog;
using SistemiPos.Infrastructure.Organizations;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Settings;
using SistemiPos.Web.Services;

namespace SistemiPos.IntegrationTests.Infrastructure;

public sealed class NavigationQueryPerformanceTests
{
    [PostgresFact]
    public async Task Catalog_Batches_Stocks_And_Request_Reads_Do_Not_Leak_Into_The_Next_Request()
    {
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        var databaseName = "sistemi_query_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source)
            { Database = "postgres", Pooling = false }.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await command.ExecuteNonQueryAsync();
        try
        {
            var counter = new ReadCounter();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(source)
                { Database = databaseName, Pooling = false }.ConnectionString).AddInterceptors(counter).Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();
            var category = new Category("Prueba", null);
            var products = Enumerable.Range(0, 40).Select(i => new Product($"P-{i}", null, $"Producto {i}", null,
                category.Id, "Marca", "Unidad", 0, 0, 10, 0, false)).ToList();
            var stock = new InventoryStock(products[0].Id);
            stock.ReplaceForSystem(7.123456m);
            var group = new BusinessGroup("GRUPO", "Prueba");
            var company = new Company(group.Id, "EMPRESA", "Prueba");
            var branch = new Branch(company.Id, "SUC", "Sucursal");
            db.AddRange(category, stock, group, company, branch);
            db.AddRange(products);
            await db.SaveChangesAsync();

            var catalog = new CatalogRepository(db);
            counter.Reset();
            var quantities = await catalog.GetStockQuantitiesAsync(products.Select(x => x.Id).ToArray());
            Assert.Equal(1, counter.Reads);
            Assert.Equal(7.123456m, quantities[products[0].Id]);
            Assert.All(products.Skip(1), product => Assert.Equal(0m, quantities[product.Id]));
            await catalog.GetStockQuantitiesAsync([]);
            Assert.Equal(1, counter.Reads);

            var settings = new SettingsRepository(db);
            counter.Reset();
            var setting = await settings.GetSystemSettingAsync();
            Assert.Same(setting, await settings.GetSystemSettingAsync());
            Assert.Equal(1, counter.Reads);
            if (setting is null)
            {
                setting = SystemSetting.CreateDefault();
                await settings.AddSystemSettingAsync(setting);
                await db.SaveChangesAsync();
            }
            counter.Reset();
            Assert.Same(setting, await settings.GetSystemSettingAsync());
            Assert.Equal(0, counter.Reads);
            await new SettingsRepository(db).GetSystemSettingAsync();
            Assert.Equal(1, counter.Reads);

            var user = new CurrentUser();
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.Request.Headers.Cookie = $".SistemiPosRestaurante.BranchContext={branch.Id}";
            http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Name, user.UserName), new Claim(ClaimTypes.Role, SecurityRoleNames.SuperAdministrator)
            ], "Test"));
            var organizations = new OrganizationRepository(db);
            var request = new HttpOperationContextAccessor(http, organizations, user);
            counter.Reset();
            Assert.Single(await request.GetAuthorizedBranchesAsync());
            var initialReads = counter.Reads;
            Assert.Equal(branch.Id, request.CurrentBranchId);
            Assert.Single(await request.GetAuthorizedBranchesAsync());
            Assert.Equal(initialReads, counter.Reads);
            db.Branches.Add(new Branch(company.Id, "SUC2", "Sucursal 2"));
            await db.SaveChangesAsync();
            Assert.Single(await request.GetAuthorizedBranchesAsync());
            var nextRequest = new HttpOperationContextAccessor(http, organizations, user);
            Assert.Equal(2, (await nextRequest.GetAuthorizedBranchesAsync()).Count);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class CurrentUser : ICurrentUserAccessor
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string UserName => "performance-test";
    }
    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public void Reset() => Reads = 0;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
