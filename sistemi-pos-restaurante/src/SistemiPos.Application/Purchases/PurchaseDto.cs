using SistemiPos.Domain.Common;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Application.Purchases;

public sealed record PurchaseDto(
    Guid Id,
    string Number,
    Guid SupplierId,
    string SupplierName,
    DateTimeOffset PurchaseDateUtc,
    string UserName,
    DocumentStatus Status,
    decimal Total,
    IReadOnlyList<PurchaseItemDto> Items,
    string? InvoiceNumber = null,
    PurchaseTermsType Terms = PurchaseTermsType.Cash,
    DateTimeOffset? DueDateUtc = null,
    Guid? PurchaseOrderId = null,
    string? Observations = null,
    string? PurchaseOrderNumber = null,
    IReadOnlyList<PurchaseTraceStageDto>? Traceability = null,
    string? SupplierNit = null,
    decimal GrossSubtotal = 0,
    decimal LineDiscountTotal = 0,
    decimal NetSubtotal = 0,
    PurchaseDiscountType GlobalDiscountType = PurchaseDiscountType.None,
    decimal GlobalDiscountValue = 0,
    decimal GlobalDiscountAmount = 0,
    Guid? BranchId = null,
    string? BranchName = null,
    Guid? WarehouseId = null,
    string? WarehouseName = null,
    DateTimeOffset? CancelledAtUtc = null,
    string? CancelledBy = null,
    string? CancellationReason = null);

public sealed record PurchaseTraceStageDto(string Stage, string UserName, DateTimeOffset OccurredAtUtc);
