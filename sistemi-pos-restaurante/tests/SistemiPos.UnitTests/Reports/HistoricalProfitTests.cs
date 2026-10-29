using SistemiPos.Application.Reports;

namespace SistemiPos.UnitTests.Reports;
public sealed class HistoricalProfitTests
{
    [Fact]
    public void Prepared_food_refund_preserves_the_cost_of_consumed_ingredients()
    {
        var row=HistoricalProfitCalculator.Calculate(100m,40m,1m,112m,112m,1m,0m);
        Assert.Equal(0m,row.RevenueWithoutTax); Assert.Equal(40m,row.Cost); Assert.Equal(-40m,row.GrossMargin);
    }
    [Fact]
    public void Physical_retail_return_reduces_historical_cost_proportionally()
    {
        var row=HistoricalProfitCalculator.Calculate(100m,10m,5m,112m,56m,5m,2.5m);
        Assert.Equal(50m,row.RevenueWithoutTax); Assert.Equal(25m,row.Cost); Assert.Equal(25m,row.GrossMargin);
    }
    [Fact]
    public void Duplicate_products_share_restoration_without_restoring_more_than_their_cost()
    {
        var row=HistoricalProfitCalculator.Calculate(50m,8m,2m,112m,0m,4m,2m);
        Assert.Equal(8m,row.Cost);
    }
    [Fact]
    public void Currency_is_never_used_to_recalculate_a_historical_cost()
    {
        var row=HistoricalProfitCalculator.Calculate(90m,6.123456m,3m,100m,0m,3m,0m);
        Assert.Equal(18.370368m,row.Cost);
    }
}
