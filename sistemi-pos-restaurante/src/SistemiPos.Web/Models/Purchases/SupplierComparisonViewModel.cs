using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Purchases;
using SistemiPos.Domain.Purchases;

namespace SistemiPos.Web.Models.Purchases;

public sealed class SupplierComparisonViewModel
{
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public IReadOnlyList<ProductSupplierDto> Rows { get; set; } = [];
    public IReadOnlyList<SelectListItem> Products { get; set; } = [];
    public IReadOnlyList<SelectListItem> Suppliers { get; set; } = [];
    public SupplierProductFormViewModel Relation { get; set; } = new();
    public string CurrencySymbol { get; set; } = "Q";
    public int CurrencyDecimals { get; set; } = 2;
}

public sealed class SupplierProductFormViewModel
{
    public Guid ProductId { get; set; }
    [Required(ErrorMessage = "El proveedor es requerido.")] public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    [Required(ErrorMessage = "El nombre de proveedor es requerido.")]
    [StringLength(300)] public string SupplierProductName { get; set; } = string.Empty;
    [StringLength(100)] public string? SupplierProductCode { get; set; }
    public SupplierPackageType PackageType { get; set; } = SupplierPackageType.Unit;
    [Range(1, int.MaxValue, ErrorMessage = "Las unidades por empaque deben ser mayores a cero.")]
    public int UnitsPerPackage { get; set; } = 1;
    public PurchaseTermsType? Terms { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Los días de crédito deben ser mayores a cero.")]
    public int? CreditDays { get; set; }
}

public sealed class SupplierPriceHistoryViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public IReadOnlyList<SupplierProductPriceHistoryDto> Rows { get; set; } = [];
    public string CurrencySymbol { get; set; } = "Q";
    public int CurrencyDecimals { get; set; } = 2;
}
