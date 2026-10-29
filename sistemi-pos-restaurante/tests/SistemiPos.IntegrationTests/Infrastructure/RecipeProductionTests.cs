using Microsoft.EntityFrameworkCore;
using Npgsql;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Recipes;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Inventory;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Recipes;
using SistemiPos.Infrastructure.Catalog;
using SistemiPos.Infrastructure.Organizations;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Recipes;
using SistemiPos.Infrastructure.Sales;

namespace SistemiPos.IntegrationTests.Infrastructure;
public sealed class RecipeProductionTests
{
    [PostgresFact]
    public async Task Production_And_Recipe_Plans_Keep_History_And_Roll_Back_Failed_Production()
    {
        var databaseName = "sistemi_production_test_" + Guid.NewGuid().ToString("N");
        var source = Environment.GetEnvironmentVariable("SISTEMI_TEST_CONNECTION");
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(source) { Database="postgres", Pooling=false }.ConnectionString);
        await admin.OpenAsync();
        await using(var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(
                new NpgsqlConnectionStringBuilder(source) { Database=databaseName, Pooling=false }.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var group = new BusinessGroup("PROTEST", "Producción"); var company = new Company(group.Id,"PROTEST","Producción");
            var branch = new Branch(company.Id,"PROTEST","Sucursal prueba"); var warehouse = new Warehouse(branch.Id,"PROTEST","Cocina",isDefault:true);
            db.AddRange(group, company, branch, warehouse);
            var category = new Category("Prueba producción", null);
            var grams = new UnitOfMeasure("gram-pro", "Gramo", UnitDimension.Mass, "g", 6);
            var unit = new UnitOfMeasure("unit-pro", "Porción", UnitDimension.Count, "u", 0);
            db.AddRange(category,grams,unit);
            Product Product(string name, ArticleType type, UnitOfMeasure measure)
            {
                var p=new Product(name,null,name,null,category.Id,"Marca",measure.Name,0,0,20,0,false);
                p.SetArticleType(type); p.SetBaseUnit(measure.Id,measure.Name); return p;
            }
            var flour=Product("Harina",ArticleType.Insumo,grams); var cheese=Product("Queso",ArticleType.Reventa,grams);
            var dough=Product("Masa",ArticleType.Subreceta,unit); var pizza=Product("Pizza",ArticleType.Preparado,unit);
            db.AddRange(flour,cheese,dough,pizza);
            var flourStock=new InventoryStock(flour.Id,warehouse.Id); flourStock.ReplaceForSystem(5000);
            var cheeseStock=new InventoryStock(cheese.Id,warehouse.Id); cheeseStock.ReplaceForSystem(1000);
            db.AddRange(flourStock,cheeseStock);
            db.ProductCostBaselines.AddRange(new ProductCostBaseline { ProductId=flour.Id, EffectiveAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1), UnitCost=0.01m },
                new ProductCostBaseline { ProductId=cheese.Id, EffectiveAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1), UnitCost=0.02m });
            var cash=new CashSession("PROTEST","tester",DateTimeOffset.UtcNow,0,branchId:branch.Id); db.Add(cash);
            await db.SaveChangesAsync();
            var context = new Scope(branch.Id,warehouse.Id); var auth=new Authorization(); var user=new User(); var clock=new Clock();
            var recipeRepository=new RecipeRepository(db);
            var recipes=new RecipeService(recipeRepository,db,new UnitConversionService(new CatalogRepository(db)),auth,user);
            var operations=new RecipeOperationsService(new RecipeOperationsRepository(db),recipeRepository,new OrganizationRepository(db),context,auth,user,clock,db);
            var doughId=await recipes.CreateRecipeAsync(new(dough.Id,72,[new(flour.Id,1000.123456m,grams.Id)]));
            var request=new ProductionRequest(Guid.NewGuid(),doughId,warehouse.Id,warehouse.Id,72,"MASA-1",null,DateTimeOffset.UtcNow);
            var orderId=await operations.CreateAsync(request);
            Assert.Equal(orderId,await operations.CreateAsync(request));
            db.ChangeTracker.Clear(); var order=await operations.OrderAsync(orderId);
            await operations.ConfirmAsync(orderId,order.Version,70,"Dos porciones perdidas");
            db.ChangeTracker.Clear();
            Assert.Equal(3999.876544m,(await db.InventoryStocks.SingleAsync(x=>x.ProductId==flour.Id)).CurrentStock);
            Assert.Equal(70m,(await db.InventoryStocks.SingleAsync(x=>x.ProductId==dough.Id)).CurrentStock);
            var confirmed=await operations.OrderAsync(orderId); Assert.Equal(2,confirmed.WasteQuantity);
            Assert.Equal(10.001235m,confirmed.TotalCost);
            var movements=await db.InventoryMovements.CountAsync();
            await operations.ConfirmAsync(orderId,0,70,"Dos porciones perdidas");
            Assert.Equal(movements,await db.InventoryMovements.CountAsync());
            var pizzaId=await recipes.CreateRecipeAsync(new(pizza.Id,1,[new(dough.Id,1,unit.Id),new(cheese.Id,100,grams.Id)]));
            db.ChangeTracker.Clear();
            // Phase 7 checks out prepared products through orders. Verify the phase 5 planning API
            // independently; the real order/payment path is covered by RestaurantOrderPersistenceTests.
            var plan = (await operations.PlanSaleAsync(pizza.Id, 2, warehouse.Id, clock.UtcNow))!;
            Assert.NotNull(plan);
            Assert.DoesNotContain(plan.Components, x => x.ProductId == flour.Id);
            Assert.Equal(200, plan.Components.Single(x => x.ProductId == cheese.Id).Quantity);
            var previousVersion = plan.VersionId;
            var current=(await recipes.GetRecipeAsync(pizzaId))!;
            await recipes.UpdateRecipeAsync(pizzaId,new(pizza.Id,1,[new(dough.Id,1,unit.Id),new(cheese.Id,150,grams.Id)],Version:current.Version));
            db.ChangeTracker.Clear();
            Assert.Equal(previousVersion, plan.VersionId);
            Assert.Equal(200, plan.Components.Single(x => x.ProductId == cheese.Id).Quantity);
            var latest=(await operations.VersionsAsync(pizzaId))[0];
            Assert.NotEqual(previousVersion, latest.Id);
            var quote=await operations.QuoteAsync(latest.Id,warehouse.Id,1);
            Assert.Equal(6.66m,quote.AvailableOutput); Assert.Contains("QUESO",quote.LimitingComponent.ToUpperInvariant());
            Assert.Equal(70,(await db.InventoryStocks.SingleAsync(x=>x.ProductId==dough.Id)).CurrentStock);
            Assert.Equal(1000,(await db.InventoryStocks.SingleAsync(x=>x.ProductId==cheese.Id)).CurrentStock);
            Assert.Equal(3999.876544m,(await db.InventoryStocks.SingleAsync(x=>x.ProductId==flour.Id)).CurrentStock);
            var failedId=await operations.CreateAsync(request with { OperationId=Guid.NewGuid(), PlannedQuantity=72000, LotNumber="NO-STOCK" });
            db.ChangeTracker.Clear(); var failed=await operations.OrderAsync(failedId); var before=await db.InventoryMovements.CountAsync();
            await Assert.ThrowsAsync<DomainRuleException>(()=>operations.ConfirmAsync(failedId,failed.Version,72000,null));
            db.ChangeTracker.Clear();
            Assert.Equal(ProductionStatus.Draft,(await db.ProductionOrders.SingleAsync(x=>x.Id==failedId)).Status);
            Assert.Equal(before,await db.InventoryMovements.CountAsync());
            context.Allowed=false;
            await Assert.ThrowsAsync<DomainRuleException>(()=>operations.OrderAsync(orderId));
            Assert.Empty(await operations.OrdersAsync());
            auth.Allowed=false;
            await Assert.ThrowsAsync<DomainRuleException>(()=>operations.ConfirmAsync(orderId,0,70,null));
        }
        finally
        {
            await using var drop=new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)",admin); await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class User : ICurrentUserAccessor { public Guid? UserId=>Guid.Parse("0b843d47-30dc-49eb-9abc-4898650ebac2"); public string UserName=>"tester"; }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow=>DateTimeOffset.UtcNow; }
    private sealed class Authorization : IOperationAuthorizationService
    {
        public bool Allowed {get;set;}=true;
        public Task<bool> HasPermissionAsync(string user,string code,CancellationToken ct=default)=>Task.FromResult(Allowed);
        public Task<string> ValidateCredentialsAsync(string user,string password,string? permission=null,CancellationToken ct=default)=>Task.FromResult(user);
    }
    private sealed class Scope(Guid branch,Guid warehouse) : IOperationContextAccessor
    {
        public bool Allowed {get;set;}=true;
        public Guid? CurrentBranchId=>branch; public Guid? CurrentWarehouseId=>warehouse;
        public Guid? CurrentCompanyId=>null; public Guid? CurrentBusinessGroupId=>null;
        public string? CurrentBranchName=>"Prueba"; public string? CurrentCompanyName=>"Prueba";
        public Task SetCurrentBranchAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<BranchDto>>(
            Allowed ? [new(branch,Guid.NewGuid(),"Prueba","TEST","Prueba",null,null,"America/Guatemala",null,true,1)] : []);
    }
}
