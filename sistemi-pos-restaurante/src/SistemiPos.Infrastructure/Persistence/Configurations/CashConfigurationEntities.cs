using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Infrastructure.Persistence.Configurations;

internal sealed class CashDenominationConfiguration : IEntityTypeConfiguration<CashDenomination>
{
    public void Configure(EntityTypeBuilder<CashDenomination> b)
    {
        b.ToTable("CashDenominations"); b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasColumnType("numeric(18,2)");
        b.HasIndex(x => new { x.Kind, x.Value }).IsUnique();
    }
}
internal sealed class PosDeviceConfiguration : IEntityTypeConfiguration<PosDevice>
{
    public void Configure(EntityTypeBuilder<PosDevice> b)
    {
        b.ToTable("PosDevices"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Provider).HasMaxLength(100); b.Property(x => x.TerminalId).HasMaxLength(100);
        b.Property(x => x.Location).HasMaxLength(150); b.Property(x => x.Observation).HasMaxLength(500);
        b.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PaymentMethodDefinitionConfiguration : IEntityTypeConfiguration<PaymentMethodDefinition>
{
    public void Configure(EntityTypeBuilder<PaymentMethodDefinition> b)
    {
        b.ToTable("PaymentMethodDefinitions"); b.HasKey(x => x.Id); b.Property(x => x.Code).HasMaxLength(30).IsRequired(); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
    }
}
internal sealed class CashMovementConceptConfiguration : IEntityTypeConfiguration<CashMovementConcept>
{
    public void Configure(EntityTypeBuilder<CashMovementConcept> b)
    {
        b.ToTable("CashMovementConcepts"); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(150).IsRequired(); b.HasIndex(x => new { x.MovementType, x.Name }).IsUnique();
    }
}
