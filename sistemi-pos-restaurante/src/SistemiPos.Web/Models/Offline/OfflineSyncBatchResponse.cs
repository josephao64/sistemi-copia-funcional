namespace SistemiPos.Web.Models.Offline;

public sealed record OfflineSyncBatchResponse(IReadOnlyList<OfflineSyncResultDto> Results);

public sealed record OfflineSyncResultDto(
    string ClientOperationId,
    string Status,
    string? SaleNumber,
    string? Error);
