using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class SalePayment : Entity
{
    public Guid SaleId { get; set; }
    public Guid? AccountPartId { get; private set; }
    public Guid? DefinitionId { get; private set; }
    public string? DefinitionName { get; private set; }
    public string? OtherReference { get; private set; }
    public static SalePayment CreateOther(decimal total, SistemiPos.Domain.Configuration.PaymentMethodDefinition definition, string user, string? reference)
    {
        if (!definition.IsActive || definition.Behavior != SistemiPos.Domain.Configuration.PaymentFinancialBehavior.OtherNonCash
            || total <= 0 || reference?.Trim().Length > 100)
            throw new DomainRuleException("Selecciona un medio no efectivo configurado y activo.");
        return new SalePayment { PaymentMethod = PaymentMethodType.Other, TotalAmount = total,
            DefinitionId = definition.Id, DefinitionName = definition.Name, CreatedByUserName = NormalizeUser(user), OtherReference = reference?.Trim() };
    }
    public void AssignAccountPart(Guid id) => AccountPartId = id;
    public PaymentMethodType PaymentMethod { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string CreatedByUserName { get; private set; } = string.Empty;
    public decimal? ReceivedAmount { get; private set; }
    public decimal? ChangeAmount { get; private set; }
    public string? TransferSourceBank { get; private set; }
    public string? TransferSenderName { get; private set; }
    public Guid? TransferBankAccountId { get; private set; }
    public string? TransferDestinationBank { get; private set; }
    public string? TransferDestinationAccountName { get; private set; }
    public string? TransferDestinationAccountNumber { get; private set; }
    public string? TransferReference { get; private set; }
    public decimal? TransferAmount { get; private set; }
    public DateTimeOffset? TransferDateUtc { get; private set; }
    public TransferPaymentStatus? TransferStatus { get; private set; }
    public string? TransferReviewedByUserName { get; private set; }
    public DateTimeOffset? TransferReviewedAtUtc { get; private set; }
    public string? TransferReviewObservation { get; private set; }
    public CardType? CardType { get; private set; }
    public string? CardLast4 { get; private set; }
    public string? CardAuthorizationNumber { get; private set; }
    public string? CardVoucherReference { get; private set; }
    public decimal? CardAmount { get; private set; }
    public CardPaymentStatus? CardStatus { get; private set; }
    public Guid? PosDeviceId { get; private set; }

    private SalePayment()
    {
    }

    public static SalePayment MixedSummary(IReadOnlyCollection<SalePayment> payments) => new()
    {
        PaymentMethod = PaymentMethodType.Mixed,
        TotalAmount = payments.Sum(x => x.TotalAmount),
        ReceivedAmount = payments.Where(x => x.PaymentMethod == PaymentMethodType.Cash).Sum(x => x.ReceivedAmount ?? 0),
        ChangeAmount = payments.Sum(x => x.ChangeAmount ?? 0)
    };

    public static SalePayment CreateCash(decimal totalAmount, decimal receivedAmount, string createdByUserName)
    {
        if (decimal.Round(receivedAmount, 2) != receivedAmount || totalAmount < 0)
            throw new DomainRuleException("El efectivo recibido debe ser un importe monetario válido.");
        if (receivedAmount < totalAmount)
        {
            throw new DomainRuleException("El monto recibido no puede ser menor al total.");
        }

        return new SalePayment
        {
            PaymentMethod = PaymentMethodType.Cash,
            TotalAmount = decimal.Round(totalAmount, 2),
            CreatedByUserName = NormalizeUser(createdByUserName),
            ReceivedAmount = decimal.Round(receivedAmount, 2),
            ChangeAmount = decimal.Round(receivedAmount - totalAmount, 2)
        };
    }

    public static SalePayment CreateTransfer(
        decimal totalAmount,
        string sourceBank,
        Guid bankAccountId,
        string destinationBank,
        string destinationAccountName,
        string destinationAccountNumber,
        string reference,
        decimal amount,
        DateTimeOffset transferDateUtc,
        TransferPaymentStatus status,
        string createdByUserName,
        string transferSenderName)
    {
        if (amount != totalAmount || totalAmount < 0 || !Enum.IsDefined(status))
            throw new DomainRuleException("El importe y estado de transferencia no son válidos.");
        if (string.IsNullOrWhiteSpace(transferSenderName)
            || bankAccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(destinationBank)
            || string.IsNullOrWhiteSpace(destinationAccountName)
            || string.IsNullOrWhiteSpace(destinationAccountNumber)
            || string.IsNullOrWhiteSpace(reference))
        {
            throw new DomainRuleException("Los datos de la transferencia son requeridos.");
        }

        return new SalePayment
        {
            PaymentMethod = PaymentMethodType.Transfer,
            TotalAmount = decimal.Round(totalAmount, 2),
            CreatedByUserName = NormalizeUser(createdByUserName),
            TransferSourceBank = string.IsNullOrWhiteSpace(sourceBank) ? null : sourceBank.Trim(),
            TransferSenderName = NameNormalizer.Normalize(transferSenderName),
            TransferBankAccountId = bankAccountId,
            TransferDestinationBank = destinationBank.Trim(),
            TransferDestinationAccountName = destinationAccountName.Trim(),
            TransferDestinationAccountNumber = destinationAccountNumber.Trim(),
            TransferReference = reference.Trim(),
            TransferAmount = decimal.Round(amount, 2),
            TransferDateUtc = transferDateUtc,
            TransferStatus = status
        };
    }

    public static SalePayment CreatePos(
        decimal totalAmount,
        CardType cardType,
        string? cardLast4,
        string authorizationNumber,
        string voucherReference,
        decimal amount,
        CardPaymentStatus status,
        string createdByUserName,
        Guid posDeviceId)
    {
        if (amount != totalAmount || totalAmount < 0 || !Enum.IsDefined(status) || !Enum.IsDefined(cardType))
            throw new DomainRuleException("El importe y estado del pago con tarjeta no son válidos.");
        if (posDeviceId == Guid.Empty)
        {
            throw new DomainRuleException("El dispositivo POS es requerido.");
        }

        if (string.IsNullOrWhiteSpace(authorizationNumber)
            || string.IsNullOrWhiteSpace(voucherReference))
        {
            throw new DomainRuleException("Los datos del pago POS son requeridos.");
        }

        return new SalePayment
        {
            PaymentMethod = PaymentMethodType.Pos,
            TotalAmount = decimal.Round(totalAmount, 2),
            CreatedByUserName = NormalizeUser(createdByUserName),
            CardType = cardType,
            CardLast4 = null,
            CardAuthorizationNumber = authorizationNumber.Trim(),
            CardVoucherReference = voucherReference.Trim(),
            CardAmount = decimal.Round(amount, 2),
            CardStatus = status,
            PosDeviceId = posDeviceId
        };
    }

    public static SalePayment CreateCredit(decimal totalAmount, string createdByUserName) => new()
    {
        PaymentMethod = PaymentMethodType.Credit,
        TotalAmount = decimal.Round(totalAmount, 2),
        CreatedByUserName = NormalizeUser(createdByUserName)
    };

    public void VerifyTransfer(string reviewedByUserName, DateTimeOffset reviewedAtUtc, string? observation)
    {
        if (PaymentMethod != PaymentMethodType.Transfer || TransferStatus is null)
        {
            throw new DomainRuleException("La venta no tiene una transferencia para verificar.");
        }

        TransferStatus = TransferPaymentStatus.Verified;
        TransferReviewedByUserName = NormalizeUser(reviewedByUserName);
        TransferReviewedAtUtc = reviewedAtUtc;
        TransferReviewObservation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
    }

    public void RejectTransfer(string reviewedByUserName, DateTimeOffset reviewedAtUtc, string? observation)
    {
        if (PaymentMethod != PaymentMethodType.Transfer || TransferStatus is null)
        {
            throw new DomainRuleException("La venta no tiene una transferencia para rechazar.");
        }

        TransferStatus = TransferPaymentStatus.Rejected;
        TransferReviewedByUserName = NormalizeUser(reviewedByUserName);
        TransferReviewedAtUtc = reviewedAtUtc;
        TransferReviewObservation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim();
    }

    public bool AllowsSaleConfirmation() =>
        PaymentMethod switch
        {
            PaymentMethodType.Cash => true,
            PaymentMethodType.Transfer => TransferStatus is TransferPaymentStatus.Received or TransferPaymentStatus.Verified,
            PaymentMethodType.Pos => CardStatus == CardPaymentStatus.Approved,
            PaymentMethodType.Credit => true,
            PaymentMethodType.Other => DefinitionId.HasValue,
            _ => false
        };

    public string? Reference =>
        PaymentMethod switch
        {
            PaymentMethodType.Transfer => TransferReference,
            PaymentMethodType.Pos => CardVoucherReference,
            PaymentMethodType.Other => OtherReference,
            _ => null
        };

    private static string NormalizeUser(string value) =>
        string.IsNullOrWhiteSpace(value) ? "system" : value.Trim();
}
