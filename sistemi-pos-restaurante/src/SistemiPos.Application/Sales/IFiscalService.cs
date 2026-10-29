using SistemiPos.Domain.Sales;

namespace SistemiPos.Application.Sales;

public sealed record FiscalCandidate(Guid SaleId, string Number, string Branch, string Currency, decimal Total,
    FiscalSubmissionStatus? Status, string? Authorization, string? Message);
public sealed record FiscalProviderResult(bool Accepted, string? Authorization, string? Series, string? Number,
    DateTimeOffset? CertifiedAtUtc, string? CertifierName, string? CertifierNit, string? CertifiedXml, string? Message);
public interface IFiscalProvider
{
    bool IsConfigured { get; }
    Task<FiscalProviderResult> CertifyAsync(string immutablePayload, string idempotencyKey, CancellationToken ct);
}
public interface IFiscalService
{
    Task<IReadOnlyList<FiscalCandidate>> ListAsync(CancellationToken ct = default);
    Task PrepareAsync(Guid saleId, CancellationToken ct = default);
}
