using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Inventory;

public sealed class PhysicalInventoryCount : AuditableEntity
{
    private readonly List<PhysicalInventoryCountCategory> _categories = [];
    private readonly List<PhysicalInventoryCountItem> _items = [];

    public string Name { get; private set; } = string.Empty;
    public PhysicalInventoryCountScope Scope { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Warehouse? Warehouse { get; set; }
    public PhysicalInventoryCountStatus Status { get; private set; } = PhysicalInventoryCountStatus.InProgress;
    public DateTimeOffset? FinalizedAtUtc { get; private set; }
    public string? FinalizedBy { get; private set; }
    public IReadOnlyCollection<PhysicalInventoryCountCategory> Categories => _categories;
    public IReadOnlyCollection<PhysicalInventoryCountItem> Items => _items;

    private PhysicalInventoryCount()
    {
    }

    public PhysicalInventoryCount(string name, PhysicalInventoryCountScope scope, Guid? warehouseId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("El nombre de la toma es requerido.");
        }

        Name = NameNormalizer.Normalize(name);
        Scope = scope;
        WarehouseId = warehouseId;
    }

    public void AssignWarehouse(Guid warehouseId)
    {
        EnsureInProgress();
        if (warehouseId == Guid.Empty)
        {
            throw new DomainRuleException("La bodega es requerida.");
        }
        WarehouseId = warehouseId;
    }

    public void IncludeCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty || _categories.Any(x => x.CategoryId == categoryId))
        {
            return;
        }

        _categories.Add(new PhysicalInventoryCountCategory(Id, categoryId));
    }

    public void IncludeProduct(Guid productId)
    {
        if (productId == Guid.Empty || _items.Any(x => x.ProductId == productId))
        {
            return;
        }

        _items.Add(new PhysicalInventoryCountItem(Id, productId));
    }

    public void EnsureInProgress()
    {
        if (Status != PhysicalInventoryCountStatus.InProgress)
        {
            throw new DomainRuleException("Una toma finalizada no puede modificarse.");
        }
    }

    public void Finalize(DateTimeOffset finalizedAtUtc, string userName)
    {
        EnsureInProgress();
        if (_items.Any(x => x.Status != PhysicalInventoryCountItemStatus.Counted))
        {
            throw new DomainRuleException("Todos los productos deben estar contados y sin reconteos pendientes.");
        }

        foreach (var item in _items)
        {
            item.CloseParticipation();
        }

        Status = PhysicalInventoryCountStatus.Finalized;
        FinalizedAtUtc = finalizedAtUtc;
        FinalizedBy = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim();
    }
}
