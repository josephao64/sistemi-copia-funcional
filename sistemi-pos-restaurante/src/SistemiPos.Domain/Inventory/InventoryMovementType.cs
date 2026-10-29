namespace SistemiPos.Domain.Inventory;

public enum InventoryMovementType
{
    PurchaseEntry = 1,
    SaleExit = 2,
    PositiveAdjustment = 3,
    NegativeAdjustment = 4,
    ReturnEntry = 5,
    CancellationReversal = 6,
    TransferExit = 7,
    TransferEntry = 8,
    PurchaseReversal = 9,
    ProductionConsumption = 10,
    ProductionEntry = 11,
    OrderConsumption = 12,
    OrderReturn = 13,
    PosOrderConsumption = 14
}
