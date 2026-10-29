using SistemiPos.Domain.Sales;

namespace SistemiPos.Application.Sales;

public sealed record SalePaymentDto(
    PaymentMethodType PaymentMethod,
    decimal TotalAmount,
    decimal? ReceivedAmount,
    decimal? ChangeAmount,
    string? TransferSourceBank,
    string? TransferDestinationBank,
    string? TransferDestinationAccountName,
    string? TransferDestinationAccountNumber,
    string? TransferReference,
    decimal? TransferAmount,
    DateTimeOffset? TransferDateUtc,
    TransferPaymentStatus? TransferStatus,
    string? TransferReviewedByUserName,
    DateTimeOffset? TransferReviewedAtUtc,
    string? TransferReviewObservation,
    CardType? CardType,
    string? CardLast4,
    string? CardAuthorizationNumber,
    string? CardVoucherReference,
    decimal? CardAmount,
    CardPaymentStatus? CardStatus,
    string? TransferSenderName = null,
    Guid? Id = null,
    int? AccountPartNumber = null,
    string? DefinitionName = null,
    string? OtherReference = null);
