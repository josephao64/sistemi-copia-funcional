using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Web.Models.Purchases;

public sealed class PurchaseOrderFormViewModel
{
    public Guid? Id { get; set; }
    public string? Number { get; set; }
    public SistemiPos.Domain.Purchases.PurchaseOrderStatus? Status { get; set; }
    [Required(ErrorMessage = "El proveedor es requerido.")]
    [Display(Name = "Proveedor")]
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    [Required(ErrorMessage = "La sucursal de destino es requerida.")]
    public Guid BranchId { get; set; }
    [Required(ErrorMessage = "La bodega de destino es requerida.")]
    public Guid WarehouseId { get; set; }

    [Display(Name = "Fecha de solicitud")]
    public DateTime RequestedAtLocal { get; set; } = DateTime.Now;

    [StringLength(1000)]
    public string? Observations { get; set; }

    public List<PurchaseOrderItemFormViewModel> Items { get; set; } = [new()];
    public IReadOnlyList<SelectListItem> Suppliers { get; set; } = [];
    public IReadOnlyList<PurchaseProductOptionViewModel> Products { get; set; } = [];
    public IReadOnlyList<PurchasePresentationOptionViewModel> Presentations { get; set; } = [];
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
    public IReadOnlyList<PurchaseWarehouseOptionViewModel> Warehouses { get; set; } = [];
}

public sealed class PurchaseOrderItemFormViewModel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SupplierInvoiceName { get; set; }
    public string? SystemDescription { get; set; }
    public string? SupplierDescription { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public decimal PackageQuantity { get; set; }
    public Guid PresentationId { get; set; }
    public string PresentationName { get; set; } = "Unidad";
    public int UnitsPerPackage { get; set; } = 1;
    public decimal BaseQuantityPerPackage { get; set; } = 1;
    public string? BaseUnitCode { get; set; }
    public decimal EquivalentUnits => decimal.Round(PackageQuantity * BaseQuantityPerPackage, 2);
}

public sealed record PurchaseProductOptionViewModel(
    Guid Id,
    string SystemName,
    string? SupplierName,
    string? SystemDescription,
    string? SupplierDescription,
    string SuggestionLabel,
    bool ManagesExpiry = false,
    decimal SalePrice = 0,
    bool AppliesTax = true,
    bool MultiPriceEnabled = false,
    IReadOnlyList<PurchasePriceRuleFormViewModel>? PriceRules = null,
    bool ManagesLots = true);

public sealed record PurchasePriceRuleFormViewModel(decimal Quantity, decimal Price);

public sealed class DirectPurchaseFormViewModel
{
    [Required(ErrorMessage = "La sucursal de destino es requerida.")]
    public Guid BranchId { get; set; }
    [Required(ErrorMessage = "La bodega de destino es requerida.")]
    public Guid WarehouseId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    [Required(ErrorMessage = "El NIT de la factura es requerido.")]
    public string SupplierNit { get; set; } = string.Empty;
    [Required(ErrorMessage = "El número de factura o documento es requerido.")]
    public string SupplierDocumentNumber { get; set; } = string.Empty;
    [Required(ErrorMessage = "La fecha de factura o compra es requerida.")]
    public DateTime SupplierDocumentDateLocal { get; set; } = DateTime.Today;
    public SistemiPos.Domain.Purchases.PurchaseTermsType Terms { get; set; } = SistemiPos.Domain.Purchases.PurchaseTermsType.Cash;
    [Range(1, int.MaxValue, ErrorMessage = "Los días de crédito deben ser mayores a cero.")]
    public int? CreditDays { get; set; }
    [StringLength(1000)]
    public string? Observations { get; set; }
    public SistemiPos.Domain.Purchases.PurchaseDiscountType GlobalDiscountType { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El descuento global no puede ser negativo.")]
    public decimal GlobalDiscountValue { get; set; }
    public List<DirectPurchaseItemFormViewModel> Items { get; set; } = [];
    public IReadOnlyList<DirectPurchaseSupplierOptionViewModel> Suppliers { get; set; } = [];
    public IReadOnlyList<PurchaseProductOptionViewModel> Products { get; set; } = [];
    public IReadOnlyList<PurchasePresentationOptionViewModel> Presentations { get; set; } = [];
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
    public IReadOnlyList<PurchaseWarehouseOptionViewModel> Warehouses { get; set; } = [];
}

public sealed class DirectPurchaseItemFormViewModel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SupplierProductName { get; set; } = string.Empty;
    public string? SupplierProductCode { get; set; }
    public SistemiPos.Domain.Purchases.SupplierPackageType PackageType { get; set; } = SistemiPos.Domain.Purchases.SupplierPackageType.Unit;
    [Range(0.01, double.MaxValue, ErrorMessage = "La cantidad de empaques debe ser mayor a cero.")]
    public decimal PackageQuantity { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Las unidades por empaque deben ser mayores a cero.")]
    public int UnitsPerPackage { get; set; } = 1;
    public Guid PresentationId { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El costo por empaque no puede ser negativo.")]
    public decimal PackageCost { get; set; }
    public decimal? LastPurchaseCost { get; set; }
    public bool ManagesExpiry { get; set; }
    public bool ManagesLots { get; set; }
    [StringLength(100, ErrorMessage = "El identificador de lote admite hasta 100 caracteres.")]
    public string? LotNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public SistemiPos.Domain.Purchases.PurchaseDiscountType DiscountType { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal DiscountValue { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El precio de venta no puede ser negativo.")]
    public decimal SalePrice { get; set; }
    public bool AppliesTax { get; set; } = true;
    public bool MultiPriceEnabled { get; set; }
    public string MultiPriceRulesJson { get; set; } = "[]";
    public bool PricingInitialized { get; set; }
}

public sealed record DirectPurchaseSupplierOptionViewModel(
    Guid Id,
    string DisplayName,
    string Nit,
    int? DefaultCreditDays,
    PurchaseTermsType DefaultTerms = PurchaseTermsType.Cash);

public sealed record PurchasePresentationOptionViewModel(Guid Id, string Name, int Units, decimal? BaseFactor = null, Guid? ProductId = null, string? BaseUnit = null);

public sealed class ReceivePurchaseOrderViewModel
{
    public Guid OrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string SupplierNit { get; set; } = string.Empty;
    [Required(ErrorMessage = "El número de factura o documento es requerido.")]
    [Display(Name = "Factura o documento del proveedor")]
    public string SupplierDocumentNumber { get; set; } = string.Empty;
    [Required]
    [Display(Name = "Fecha del documento")]
    public DateTime SupplierDocumentDateLocal { get; set; } = DateTime.Today;
    [Display(Name = "Condición de compra")]
    public SistemiPos.Domain.Purchases.PurchaseTermsType Terms { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Los días de crédito deben ser mayores a cero.")]
    public int? CreditDays { get; set; }
    [StringLength(1000)]
    public string? Observations { get; set; }
    public SistemiPos.Domain.Purchases.PurchaseDiscountType GlobalDiscountType { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El descuento global no puede ser negativo.")]
    public decimal GlobalDiscountValue { get; set; }
    public List<ReceivePurchaseOrderItemViewModel> Items { get; set; } = [];
    public IReadOnlyList<PurchaseProductOptionViewModel> Products { get; set; } = [];
    public IReadOnlyList<PurchasePresentationOptionViewModel> Presentations { get; set; } = [];
}

public sealed class ReceivePurchaseOrderItemViewModel
{
    public Guid ItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SupplierInvoiceName { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal PendingQuantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "La cantidad no puede ser negativa.")]
    public decimal Quantity { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
    public decimal ActualUnitCost { get; set; }
    public Guid ProductId { get; set; }
    public bool IsAdditional { get; set; }
    public bool ManagesExpiry { get; set; }
    public bool ManagesLots { get; set; }
    [StringLength(100, ErrorMessage = "El identificador de lote admite hasta 100 caracteres.")]
    public string? LotNumber { get; set; }
    [Display(Name = "Fecha de caducidad")]
    public DateOnly? ExpiryDate { get; set; }
    public string SupplierProductName { get; set; } = string.Empty;
    public string? SupplierProductCode { get; set; }
    public SistemiPos.Domain.Purchases.SupplierPackageType PackageType { get; set; } = SistemiPos.Domain.Purchases.SupplierPackageType.Unit;
    [Range(0, double.MaxValue, ErrorMessage = "La cantidad de empaques no puede ser negativa.")]
    public decimal PackageQuantity { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Las unidades por empaque deben ser mayores a cero.")]
    public int UnitsPerPackage { get; set; } = 1;
    public string PresentationName { get; set; } = "Unidad";
    public Guid PresentationId { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El costo por empaque no puede ser negativo.")]
    public decimal PackageCost { get; set; }
    public decimal? LastPurchaseCost { get; set; }
    public SistemiPos.Domain.Purchases.PurchaseDiscountType DiscountType { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El descuento no puede ser negativo.")]
    public decimal DiscountValue { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El precio de venta no puede ser negativo.")]
    public decimal SalePrice { get; set; }
    public bool AppliesTax { get; set; } = true;
    public bool MultiPriceEnabled { get; set; }
    public string MultiPriceRulesJson { get; set; } = "[]";
    public bool PricingInitialized { get; set; }
}

public sealed class PurchaseCapturePartialViewModel
{
    public bool IsDirectPurchase { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public string SupplierNit { get; init; } = string.Empty;
    public DateTime SupplierDocumentDateLocal { get; init; }
    public string SupplierDocumentNumber { get; init; } = string.Empty;
    public PurchaseTermsType Terms { get; init; }
    public int? CreditDays { get; init; }
    public PurchaseDiscountType GlobalDiscountType { get; init; }
    public decimal GlobalDiscountValue { get; init; }
    public string? Observations { get; init; }
    public IReadOnlyList<PurchaseCaptureLineViewModel> Items { get; init; } = [];
    public IReadOnlyList<DirectPurchaseSupplierOptionViewModel> Suppliers { get; init; } = [];
    public IReadOnlyList<PurchaseProductOptionViewModel> Products { get; init; } = [];
    public IReadOnlyList<PurchasePresentationOptionViewModel> Presentations { get; init; } = [];
    public IReadOnlyList<SelectListItem> Branches { get; init; } = [];
    public IReadOnlyList<PurchaseWarehouseOptionViewModel> Warehouses { get; init; } = [];
    public string CurrencySymbol { get; init; } = "Q";
    public int CurrencyDecimals { get; init; } = 2;
    public decimal TaxPercent { get; init; }
}

public sealed class PurchaseCaptureLineViewModel
{
    public Guid ItemId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal RequestedQuantity { get; init; }
    public bool IsAdditional { get; init; }
    public bool ManagesExpiry { get; init; }
    public bool ManagesLots { get; init; }
    public string? LotNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public Guid PresentationId { get; init; }
    public string PresentationName { get; init; } = "Unidad";
    public SupplierPackageType PackageType { get; init; } = SupplierPackageType.Unit;
    public decimal PackageQuantity { get; init; }
    public int UnitsPerPackage { get; init; } = 1;
    public decimal PackageCost { get; init; }
    public decimal? LastPurchaseCost { get; init; }
    public PurchaseDiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public string SupplierProductName { get; init; } = string.Empty;
    public string? SupplierProductCode { get; init; }
    public decimal SalePrice { get; init; }
    public bool AppliesTax { get; init; } = true;
    public bool MultiPriceEnabled { get; init; }
    public string MultiPriceRulesJson { get; init; } = "[]";
    public bool PricingInitialized { get; init; }
}

public sealed record PurchaseWarehouseOptionViewModel(Guid Id, Guid BranchId, string Name);
