namespace SistemiPos.Application.Sales;

public sealed record SaleItemDto(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxableBaseAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal Subtotal,
    bool IsTaxExempt,
    decimal GrossAmount = 0m, Guid? RecipeVersionId = null, int? RecipeVersionNumber = null);
