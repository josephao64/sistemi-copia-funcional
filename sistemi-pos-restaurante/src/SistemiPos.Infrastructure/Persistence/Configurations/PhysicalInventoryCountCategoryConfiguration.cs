using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Inventory;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class PhysicalInventoryCountCategoryConfiguration : IEntityTypeConfiguration<PhysicalInventoryCountCategory>
{
    public void Configure(EntityTypeBuilder<PhysicalInventoryCountCategory> builder)
    {
        builder.ToTable("PhysicalInventoryCountCategories");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PhysicalInventoryCountId, x.CategoryId }).IsUnique();

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
