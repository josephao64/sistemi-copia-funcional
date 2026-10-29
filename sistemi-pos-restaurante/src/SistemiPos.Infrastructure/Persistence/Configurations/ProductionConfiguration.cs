using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Recipes;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class RecipeVersionConfiguration : IEntityTypeConfiguration<RecipeVersion>
{
    public void Configure(EntityTypeBuilder<RecipeVersion> b)
    {
        b.ToTable("RecipeVersions"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.RecipeId, x.Number }).IsUnique();
        b.Property(x => x.DefinitionJson).HasColumnType("jsonb");
        b.HasOne<Recipe>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class ProductCostBaselineConfiguration : IEntityTypeConfiguration<ProductCostBaseline>
{
    public void Configure(EntityTypeBuilder<ProductCostBaseline> b)
    {
        b.ToTable("ProductCostBaselines"); b.HasKey(x => x.ProductId);
        b.Property(x => x.UnitCost).HasPrecision(18, 6);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class ProductionOrderConfiguration : IEntityTypeConfiguration<ProductionOrder>
{
    public void Configure(EntityTypeBuilder<ProductionOrder> b)
    {
        b.ToTable("ProductionOrders", t => t.HasCheckConstraint("CK_Production_Yield", "\"PlannedQuantity\" > 0 AND (\"ActualQuantity\" IS NULL OR \"ActualQuantity\" >= 0)"));
        b.HasKey(x => x.Id); b.HasIndex(x => x.OperationId).IsUnique(); b.HasIndex(x => x.Number).IsUnique();
        b.Property(x => x.Number).HasMaxLength(50); b.Property(x => x.LotNumber).HasMaxLength(100);
        b.Property(x => x.VarianceReason).HasMaxLength(500);
        b.Property(x => x.ResponsibleUser).HasMaxLength(256); b.Property(x => x.ConfirmedBy).HasMaxLength(256);
        b.Property(x => x.CreatedBy).HasMaxLength(256); b.Property(x => x.ModifiedBy).HasMaxLength(256);
        b.Property(x => x.PlannedQuantity).HasPrecision(18, 6); b.Property(x => x.ActualQuantity).HasPrecision(18, 6);
        b.Property(x => x.WasteQuantity).HasPrecision(18, 6); b.Property(x => x.TotalCost).HasPrecision(18, 6);
        b.Property(x => x.UnitCost).HasPrecision(18, 6); b.Property(x => x.Version).IsRowVersion();
        b.Property(x => x.ConsumptionJson).HasColumnType("jsonb");
        b.HasOne(x => x.RecipeVersion).WithMany().HasForeignKey(x => x.RecipeVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.OriginWarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
