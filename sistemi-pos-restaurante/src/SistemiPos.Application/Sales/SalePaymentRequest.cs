using SistemiPos.Domain.Sales;

namespace SistemiPos.Application.Sales;

public sealed record SalePaymentRequest(
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
    Guid? PosDeviceId = null,
    string? TransferSenderName = null,
    decimal? AppliedAmount = null,
    int? AccountPartNumber = null,
    Guid? DefinitionId = null,
    string? OtherReference = null);
