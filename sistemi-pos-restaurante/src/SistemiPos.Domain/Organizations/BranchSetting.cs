using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Organizations;

public sealed class BranchSetting : AuditableEntity
{
    public Guid BranchId { get; private set; }
    public Branch Branch { get; set; } = null!;

    public string? EstablishmentCode { get; private set; }
    public string? EstablishmentName { get; private set; }
    public string? ReceiptFooter { get; private set; }

    public bool OverridePaymentMethods { get; private set; }
    public bool CashPaymentsEnabled { get; private set; } = true;
    public bool TransferPaymentsEnabled { get; private set; } = true;
    public bool PosPaymentsEnabled { get; private set; } = true;
    public bool CreditPaymentsEnabled { get; private set; } = true;

    private BranchSetting()
    {
    }

    public BranchSetting(
        Guid branchId,
        string? establishmentCode = null,
        string? establishmentName = null,
        string? receiptFooter = null,
        bool overridePaymentMethods = false,
        bool cashPaymentsEnabled = true,
        bool transferPaymentsEnabled = true,
        bool posPaymentsEnabled = true,
        bool creditPaymentsEnabled = true)
    {
        if (branchId == Guid.Empty)
        {
            throw new DomainRuleException("La sucursal es requerida para la configuración.");
        }

        BranchId = branchId;
        Update(establishmentCode, establishmentName, receiptFooter, overridePaymentMethods, cashPaymentsEnabled, transferPaymentsEnabled, posPaymentsEnabled, creditPaymentsEnabled);
    }

    public void Update(
        string? establishmentCode,
        string? establishmentName,
        string? receiptFooter,
        bool overridePaymentMethods,
        bool cashPaymentsEnabled,
        bool transferPaymentsEnabled,
        bool posPaymentsEnabled,
        bool creditPaymentsEnabled)
    {
        EstablishmentCode = string.IsNullOrWhiteSpace(establishmentCode) ? null : establishmentCode.Trim();
        EstablishmentName = string.IsNullOrWhiteSpace(establishmentName) ? null : NameNormalizer.Normalize(establishmentName);
        ReceiptFooter = string.IsNullOrWhiteSpace(receiptFooter) ? null : receiptFooter.Trim();
        OverridePaymentMethods = overridePaymentMethods;
        CashPaymentsEnabled = cashPaymentsEnabled;
        TransferPaymentsEnabled = transferPaymentsEnabled;
        PosPaymentsEnabled = posPaymentsEnabled;
        CreditPaymentsEnabled = creditPaymentsEnabled;
    }

    public void ConfigurePaymentMethods(bool overridePayments, bool cash, bool transfer, bool pos, bool credit)
    {
        OverridePaymentMethods = overridePayments;
        CashPaymentsEnabled = cash;
        TransferPaymentsEnabled = transfer;
        PosPaymentsEnabled = pos;
        CreditPaymentsEnabled = credit;
    }
}
