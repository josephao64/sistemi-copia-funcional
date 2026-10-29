using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Audit;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecordType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RecordId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DocumentNumber).HasMaxLength(50);
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.OccurredAtUtc).IsRequired();

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.BranchId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.CompanyId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.Module, x.RecordType, x.RecordId });
    }
}
