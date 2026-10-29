using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Catalog;
using SistemiPos.Application.Customers;
using SistemiPos.Domain.Sales;
using System.ComponentModel.DataAnnotations;

namespace SistemiPos.Web.Models.Sales;

public sealed class SaleFormViewModel
{
    public Guid CashSessionId { get; set; }
    public string? CurrentCashRegisterCode { get; set; }
    public decimal? ExpectedTotal { get; set; }
    public Guid? TerminalId { get; set; }
    [Required, StringLength(80)] public string OperationId { get; set; } = "SALE-" + Guid.NewGuid().ToString("N");
    public IReadOnlyList<SelectListItem> Terminals { get; set; } = [];
    public Guid? CustomerId { get; set; }

    [StringLength(50, ErrorMessage = "El NIT no puede superar 50 caracteres.")]
    [Display(Name = "NIT del cliente")]
    public string? CustomerNit { get; set; } = "C/F";

    [StringLength(200, ErrorMessage = "El nombre no puede superar 200 caracteres.")]
    [Display(Name = "Razón social / nombre")]
    public string? CustomerName { get; set; }
    [StringLength(500, ErrorMessage = "La dirección no puede superar 500 caracteres.")]
    [Display(Name = "Dirección del cliente")]
    public string? CustomerAddress { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerVisitOrigin { get; set; }
    public string? DiscountReason { get; set; }
    public string? DiscountAuthorizedByUserName { get; set; }
    public string? DiscountAuthorizerPassword { get; set; }
    public Guid? DiscountApprovalRequestId { get; set; }
    public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;
    public decimal? ReceivedAmount { get; set; }
    public string? TransferSourceBank { get; set; }
    public string? TransferSenderName { get; set; }
    public Guid? TransferBankAccountId { get; set; }
    public string? TransferReference { get; set; }
    public decimal? TransferAmount { get; set; }
    public DateTime TransferDateLocal { get; set; } = DateTime.Now;
    public TransferPaymentStatus TransferStatus { get; set; } = TransferPaymentStatus.Received;
    public CardType CardType { get; set; } = CardType.Debit;
    public string? CardLast4 { get; set; }
    public string? CardAuthorizationNumber { get; set; }
    public string? CardVoucherReference { get; set; }
    public decimal? CardAmount { get; set; }
    public CardPaymentStatus CardStatus { get; set; } = CardPaymentStatus.Approved;
    public Guid? PosDeviceId { get; set; }
    public DateTime CreditDueDateLocal { get; set; } = DateTime.Today.AddDays(30);
    public bool CashPaymentsEnabled { get; set; } = true;
    public bool TransferPaymentsEnabled { get; set; } = true;
    public bool PosPaymentsEnabled { get; set; } = true;
    public bool CreditPaymentsEnabled { get; set; }
    public string CurrencySymbol { get; set; } = "Q";
    public int CurrencyDecimals { get; set; } = 2;
    public List<SaleItemFormViewModel> Items { get; set; } = [];
    public IReadOnlyList<SelectListItem> CashSessions { get; set; } = [];
    public IReadOnlyList<SelectListItem> Customers { get; set; } = [];
    public IReadOnlyList<CustomerDto> CustomerCatalog { get; set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; set; } = [];
    public IReadOnlyList<ProductDto> ProductCatalog { get; set; } = [];
    public IReadOnlyList<SelectListItem> BankAccounts { get; set; } = [];
    public IReadOnlyList<SelectListItem> OriginBanks { get; set; } = [];
    public IReadOnlyList<SelectListItem> PosDevices { get; set; } = [];
}
