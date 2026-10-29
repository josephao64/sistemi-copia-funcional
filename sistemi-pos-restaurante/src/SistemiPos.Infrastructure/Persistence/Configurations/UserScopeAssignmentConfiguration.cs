using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class UserScopeAssignmentConfiguration : IEntityTypeConfiguration<UserScopeAssignment>
{
    public void Configure(EntityTypeBuilder<UserScopeAssignment> builder)
    {
        builder.ToTable("UserScopeAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.ScopeLevel).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.BranchId });

        builder.HasOne(x => x.BusinessGroup)
            .WithMany()
            .HasForeignKey(x => x.BusinessGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
