namespace SistemiPos.Application.Reports;

public sealed record HistoricalProfit(decimal RevenueWithoutTax, decimal Cost, decimal GrossMargin);
public static class HistoricalProfitCalculator
{
    public static HistoricalProfit Calculate(decimal lineBase, decimal historicalUnitCost, decimal lineQuantity,
        decimal originalSaleTotal, decimal refundedAmount, decimal productSoldQuantity, decimal productRestoredQuantity)
    {
        if (lineQuantity <= 0 || productSoldQuantity <= 0 || historicalUnitCost < 0 || originalSaleTotal < 0 || refundedAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(lineQuantity), "Los importes y cantidades históricos no son válidos.");
        var refundRatio = originalSaleTotal == 0 ? 0 : Math.Clamp(refundedAmount / originalSaleTotal,0,1);
        var restoredRatio = Math.Clamp(productRestoredQuantity / productSoldQuantity,0,1);
        var revenue = decimal.Round(lineBase * (1-refundRatio),2);
        var cost = decimal.Round(historicalUnitCost * lineQuantity * (1-restoredRatio),6);
        return new(revenue,cost,revenue-cost);
    }
}
