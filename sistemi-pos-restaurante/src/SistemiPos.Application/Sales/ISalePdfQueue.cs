namespace SistemiPos.Application.Sales;

public sealed record SalePdfJobDto(Guid Id, Guid SaleId, string SaleNumber, string Status, int Attempts);
public sealed record SalePdfDownload(string SaleNumber, byte[] Content);

public interface ISalePdfQueue
{
    Task<SalePdfJobDto> RequestAsync(Guid saleId, string requestKey, CancellationToken ct = default);
    Task<SalePdfJobDto?> GetAsync(Guid jobId, CancellationToken ct = default);
    Task<SalePdfDownload?> DownloadAsync(Guid jobId, CancellationToken ct = default);
}
