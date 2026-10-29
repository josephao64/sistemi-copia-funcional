using SistemiPos.Domain.Sales;

namespace SistemiPos.Web.Models.Offline;

public sealed record OfflineSyncBatchRequest(IReadOnlyList<OfflineSaleSyncRequest> Sales);

public sealed record OfflineSaleSyncRequest(
    string ClientOperationId,
    DateTimeOffset LocalCreatedAtUtc,
    Guid CashSessionId,
    Guid? CustomerId,
    string? CustomerNit,
    string? CustomerName,
    PaymentMethodType PaymentMethod,
    decimal? ReceivedAmount,
    string? TransferSourceBank,
    Guid? TransferBankAccountId,
    string? TransferReference,
    decimal? TransferAmount,
    DateTimeOffset? TransferDateUtc,
    TransferPaymentStatus? TransferStatus,
    CardType? CardType,
    string? CardLast4,
    string? CardAuthorizationNumber,
    string? CardVoucherReference,
    decimal? CardAmount,
    CardPaymentStatus? CardStatus,
    IReadOnlyList<OfflineSaleItemSyncRequest> Items,
    string? CustomerAddress = null,
    Guid? PosDeviceId = null,
    string? TransferSenderName = null,
    string? CustomerPhone = null,
    string? CustomerVisitOrigin = null,
    string? OfflineTicket = null,
    decimal? ExpectedTotal = null);

public sealed record OfflineSaleItemSyncRequest(
    Guid ProductId,
    decimal Quantity,
    decimal DiscountPercent);
