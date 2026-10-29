using SistemiPos.Domain.Sales;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Sales;
public sealed class FiscalSubmissionTests
{
    [Fact]
    public void An_uncertain_external_result_blocks_a_blind_resubmission()
    {
        var document=new FiscalSubmission(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"{\"Total\":100}");
        document.Begin(); document.Complete(FiscalSubmissionStatus.ReconciliationRequired);
        Assert.Throws<DomainRuleException>(()=>document.Begin());
        Assert.Equal("{\"Total\":100}",document.PayloadJson);
    }
    [Fact]
    public void Stable_key_is_scoped_to_company_and_sale()
    {
        var sale=Guid.NewGuid();var company=Guid.NewGuid();
        var first=new FiscalSubmission(sale,company,Guid.NewGuid(),"{}");
        var same=new FiscalSubmission(sale,company,Guid.NewGuid(),"{}");
        var other=new FiscalSubmission(sale,Guid.NewGuid(),Guid.NewGuid(),"{}");
        Assert.Equal(first.IdempotencyKey,same.IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey,other.IdempotencyKey);
    }
}
