using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Inventory;

public sealed class PhysicalInventoryCountItem : AuditableEntity
{
    public Guid PhysicalInventoryCountId { get; private set; }
    public PhysicalInventoryCount PhysicalInventoryCount { get; set; } = null!;
    public Guid ProductId { get; private set; }
    public Product Product { get; set; } = null!;
    public PhysicalInventoryCountItemStatus Status { get; private set; } = PhysicalInventoryCountItemStatus.Pending;
    public bool IsActiveParticipation { get; private set; } = true;
    public decimal? ReferenceStock { get; private set; }
    public uint? ReferenceStockVersion { get; private set; }
    public decimal? CountedStock { get; private set; }
    public decimal? Difference { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset? CountingStartedAtUtc { get; private set; }
    public string? CountingStartedBy { get; private set; }
    public DateTimeOffset? CountedAtUtc { get; private set; }
    public string? CountedBy { get; private set; }
    public decimal? OperationalStockBeforeAdjustment { get; private set; }
    public decimal? AppliedAdjustment { get; private set; }
    public decimal? OperationalStockAfterAdjustment { get; private set; }

    private PhysicalInventoryCountItem()
    {
    }

    public PhysicalInventoryCountItem(Guid physicalInventoryCountId, Guid productId)
    {
        if (physicalInventoryCountId == Guid.Empty || productId == Guid.Empty)
        {
            throw new DomainRuleException("La toma y el producto son requeridos.");
        }

        PhysicalInventoryCountId = physicalInventoryCountId;
        ProductId = productId;
    }

    public void BeginCounting(decimal referenceStock, uint referenceStockVersion, DateTimeOffset startedAtUtc, string userName)
    {
        if (!IsActiveParticipation)
        {
            throw new DomainRuleException("El producto pertenece a una toma finalizada.");
        }

        ReferenceStock = referenceStock;
        ReferenceStockVersion = referenceStockVersion;
        CountedStock = null;
        Difference = null;
        Comment = null;
        CountedAtUtc = null;
        CountedBy = null;
        CountingStartedAtUtc = startedAtUtc;
        CountingStartedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        Status = PhysicalInventoryCountItemStatus.Pending;
    }

    public bool SaveCount(int countedStock, string? comment, uint currentStockVersion, DateTimeOffset countedAtUtc, string userName)
    {
        if (countedStock < 0)
        {
            throw new DomainRuleException("La existencia contada debe ser un número entero igual o mayor que cero.");
        }

        if (ReferenceStock is null || ReferenceStockVersion is null)
        {
            throw new DomainRuleException("Inicie el conteo del producto antes de guardarlo.");
        }

        if (ReferenceStockVersion.Value != currentStockVersion)
        {
            CountedStock = null;
            Difference = null;
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            CountedAtUtc = null;
            CountedBy = null;
            Status = PhysicalInventoryCountItemStatus.RecountRequired;
            return false;
        }

        CountedStock = countedStock;
        Difference = countedStock - ReferenceStock.Value;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CountedAtUtc = countedAtUtc;
        CountedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
        Status = PhysicalInventoryCountItemStatus.Counted;
        return true;
    }

    public void RecordAdjustment(decimal before, decimal adjustment, decimal after)
    {
        OperationalStockBeforeAdjustment = before;
        AppliedAdjustment = adjustment;
        OperationalStockAfterAdjustment = after;
    }

    public void CloseParticipation() => IsActiveParticipation = false;
}
