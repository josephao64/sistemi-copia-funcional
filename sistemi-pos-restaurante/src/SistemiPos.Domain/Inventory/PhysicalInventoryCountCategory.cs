using SistemiPos.Domain.Catalog;
using SistemiPos.Domain.Common;

namespace SistemiPos.Domain.Inventory;

public sealed class PhysicalInventoryCountCategory : Entity
{
    public Guid PhysicalInventoryCountId { get; private set; }
    public PhysicalInventoryCount PhysicalInventoryCount { get; set; } = null!;
    public Guid CategoryId { get; private set; }
    public Category Category { get; set; } = null!;

    private PhysicalInventoryCountCategory()
    {
    }

    public PhysicalInventoryCountCategory(Guid physicalInventoryCountId, Guid categoryId)
    {
        PhysicalInventoryCountId = physicalInventoryCountId;
        CategoryId = categoryId;
    }
}
