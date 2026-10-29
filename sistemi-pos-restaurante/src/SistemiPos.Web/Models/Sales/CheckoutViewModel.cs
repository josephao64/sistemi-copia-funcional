using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Sales;
using SistemiPos.Domain.Sales;

namespace SistemiPos.Web.Models.Sales;

public sealed class CheckoutViewModel
{
    public Guid CashSessionId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? TerminalId { get; set; }
    [Required, StringLength(80)] public string OperationId { get; set; } = "SALE-" + Guid.NewGuid().ToString("N");
    public Guid? CustomerId { get; set; }
    public string? CustomerNit { get; set; } = "C/F";
    public string? CustomerName { get; set; }
    public string? CustomerAddress { get; set; }
    public Guid? ApprovalId { get; set; }
    public string? DiscountReason { get; set; }
    public List<CheckoutItemViewModel> Items { get; set; } = [];
    public List<CheckoutPaymentViewModel> Payments { get; set; } = [new()];
    public string SplitMode { get; set; } = "None";
    [Range(1, 10)] public int People { get; set; } = 2;
    public List<decimal> Amounts { get; set; } = [];
    public CheckoutQuote? Quote { get; set; }
    public decimal? ExpectedTotal { get; set; }
    public string SymbolPosition { get; set; } = "Before";
    public string DecimalSeparator { get; set; } = ".";
    public string ThousandsSeparator { get; set; } = ",";
    public IReadOnlyList<SelectListItem> Banks { get; set; } = [];
    public IReadOnlyList<SelectListItem> Devices { get; set; } = [];
    public IReadOnlyList<SelectListItem> OtherMethods { get; set; } = [];
    public IReadOnlyList<SelectListItem> Terminals { get; set; } = [];
    public string CurrencySymbol { get; set; } = string.Empty;
    public int CurrencyDecimals { get; set; } = 2;
}
public sealed class CheckoutItemViewModel
{
    public Guid ProductId { get; set; }
    public Guid? OrderLineId { get; set; }
    [Range(typeof(decimal), "0", "999999999999")] public decimal Quantity { get; set; }
    public SaleDiscountType DiscountType { get; set; } = SaleDiscountType.Percentage;
    public decimal DiscountValue { get; set; }
    public SaleItemRequest ToRequest() => new(ProductId, Quantity, 0, DiscountType, DiscountValue, OrderLineId: OrderLineId);
}
public sealed class CheckoutPaymentViewModel
{
    public PaymentMethodType Method { get; set; } = PaymentMethodType.Cash;
    public decimal Amount { get; set; }
    public decimal? Received { get; set; }
    public int? PartNumber { get; set; }
    public Guid? BankId { get; set; }
    public string? Sender { get; set; }
    public string? Reference { get; set; }
    public Guid? DeviceId { get; set; }
    public string? Authorization { get; set; }
    public string? Voucher { get; set; }
    public CardType CardType { get; set; } = CardType.Debit;
    public Guid? DefinitionId { get; set; }
    public SalePaymentRequest ToRequest(decimal singleTotal, bool single) => new(Method, Received, null, BankId, Reference,
        Method == PaymentMethodType.Transfer ? single ? singleTotal : Amount : null, null, TransferPaymentStatus.Received,
        CardType, null, Authorization, Voucher, Method == PaymentMethodType.Pos ? single ? singleTotal : Amount : null,
        CardPaymentStatus.Approved, DeviceId, Sender, Amount, PartNumber, DefinitionId, Method == PaymentMethodType.Other ? Reference : null);
}
