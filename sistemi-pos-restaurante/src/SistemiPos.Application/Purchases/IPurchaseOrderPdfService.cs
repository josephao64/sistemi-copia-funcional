namespace SistemiPos.Application.Purchases;

public interface IPurchaseOrderPdfService
{
    Task<byte[]> GenerateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
