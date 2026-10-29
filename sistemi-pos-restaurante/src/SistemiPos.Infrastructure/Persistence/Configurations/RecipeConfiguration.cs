using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes", table => table.HasCheckConstraint("CK_Recipes_Yield", "\"YieldQuantity\" > 0"));
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ProductId).IsUnique();
        builder.Property(x => x.YieldQuantity).HasPrecision(18, 6);
        builder.Property(x => x.YieldUnitCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Instructions).HasMaxLength(4000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.Version).IsRowVersion();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.YieldUnit).WithMany().HasForeignKey(x => x.YieldUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Ingredients).WithOne().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Ingredients).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients", table => table.HasCheckConstraint("CK_RecipeIngredients_Quantities",
            "\"Quantity\" > 0 AND \"BaseQuantity\" > 0"));
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RecipeId, x.ProductId }).IsUnique();
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.BaseQuantity).HasPrecision(18, 6);
        builder.Property(x => x.UnitCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.BaseUnitCode).HasMaxLength(30).IsRequired();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subrecipe).WithMany().HasForeignKey(x => x.SubrecipeId).OnDelete(DeleteBehavior.Restrict);
    }
}
