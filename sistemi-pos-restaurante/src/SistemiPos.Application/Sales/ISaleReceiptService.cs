namespace SistemiPos.Application.Sales;

public interface ISaleReceiptService
{
    Task<string> GenerateTicketAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateProformaPdfAsync(ProformaRequest request, CancellationToken cancellationToken = default);
}
