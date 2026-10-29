using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public enum FiscalSubmissionStatus { PendingConfiguration = 1, Ready, Submitting, ReconciliationRequired, Rejected, Certified }
public sealed class FiscalSubmission : AuditableEntity
{
    public Guid SaleId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid BranchId { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    public string PayloadJson { get; private set; } = "";
    public FiscalSubmissionStatus Status { get; private set; }
    public string? Message { get; private set; }
    public uint Version { get; private set; }
    private FiscalSubmission() { }
    public FiscalSubmission(Guid sale, Guid company, Guid branch, string payload)
    {
        if (sale == Guid.Empty || company == Guid.Empty || branch == Guid.Empty)
            throw new DomainRuleException("El documento fiscal requiere venta y ubicación válidas.");
        SaleId = sale; CompanyId = company; BranchId = branch;
        IdempotencyKey = $"FEL-{company:N}-{sale:N}"; PayloadJson = payload;
        Status = FiscalSubmissionStatus.PendingConfiguration;
    }
    public void Begin()
    {
        if (Status is FiscalSubmissionStatus.Submitting or FiscalSubmissionStatus.ReconciliationRequired or FiscalSubmissionStatus.Certified)
            throw new DomainRuleException("Consulta o concilia el resultado anterior antes de solicitar otra certificación.");
        Status = FiscalSubmissionStatus.Submitting; Message = null;
    }
    public void Complete(FiscalSubmissionStatus state, string? message = null)
    {
        if (Status is FiscalSubmissionStatus.Certified or FiscalSubmissionStatus.ReconciliationRequired
            || (Status == FiscalSubmissionStatus.Submitting && state == FiscalSubmissionStatus.PendingConfiguration))
            throw new DomainRuleException("El resultado fiscal requiere conciliación y no puede reiniciarse.");
        Status = state; Message = message?.Length > 500 ? message[..500] : message;
    }
}
