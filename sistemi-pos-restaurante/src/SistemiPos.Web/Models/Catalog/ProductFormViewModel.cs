using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Catalog;
using SistemiPos.Domain.Catalog;

namespace SistemiPos.Web.Models.Catalog;

public sealed class ProductFormViewModel
{
    public Guid? Id { get; set; }

    [Display(Name = "Código")]
    public string InternalCode { get; set; } = string.Empty;

    [Display(Name = "Código de barras")]
    public string? Barcode { get; set; }

    [Required(ErrorMessage = "El nombre es requerido.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Tipo de artículo")]
    public ArticleType ArticleType { get; set; } = ArticleType.Insumo;

    [Required(ErrorMessage = "La unidad base es requerida.")]
    [Display(Name = "Unidad base")]
    public Guid? BaseUnitId { get; set; }

    [StringLength(300)]
    [Display(Name = "Nombre de proveedor")]
    public string? SupplierInvoiceName { get; set; }

    [StringLength(800)]
    [Display(Name = "Descripción del proveedor")]
    public string? SupplierDescription { get; set; }

    [Display(Name = "Producto base")]
    public Guid? ParentProductId { get; set; }

    [Display(Name = "Valor de variante")]
    public string? VariantValue { get; set; }

    [Display(Name = "Este producto maneja variantes")]
    public bool ManagesVariants { get; set; }

    [Display(Name = "Maneja caducidad")]
    public bool ManagesExpiry { get; set; }

    [Display(Name = "Control por lote")]
    public bool ManagesLots { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "Los días de alerta deben ser mayores a cero.")]
    [Display(Name = "Alertar antes de caducar (días)")]
    public int? ExpiryAlertDays { get; set; }

    public List<VariantGroupFormViewModel> VariantGroups { get; set; } = [];
    public List<ProductCombinationFormViewModel> Combinations { get; set; } = [];
    public List<ProductAttributeFormViewModel> Attributes { get; set; } = [];
    public IReadOnlyList<VariantTypeDto> VariantCatalog { get; set; } = [];

    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Display(Name = "Imagen del producto")]
    public IFormFile? ImageFile { get; set; }
    public string? ImageUrl { get; set; }

    [Display(Name = "Categoría")]
    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    [Display(Name = "Marca")]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "La unidad de medida es requerida.")]
    [Display(Name = "Unidad de medida")]
    public string UnitOfMeasure { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
    [Display(Name = "Último costo")]
    public decimal LastCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El valor debe ser positivo.")]
    [Display(Name = "Costo anterior")]
    public decimal PreviousCost { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El valor debe ser positivo.")]
    [Display(Name = "Precio de venta general")]
    public decimal SalePrice { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El valor debe ser positivo.")]
    [Display(Name = "Stock mínimo")]
    public decimal MinimumStock { get; set; }

    [Display(Name = "Aplica impuesto")]
    public bool AppliesTax { get; set; } = true;

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Habilitar multiprecios")]
    public bool MultiPriceEnabled { get; set; }

    [Display(Name = "Tipo")]
    public MultiPriceMode MultiPriceMode { get; set; } = MultiPriceMode.MinimumQuantity;

    public List<ProductPriceRuleFormViewModel> PriceRules { get; set; } = [];

    [Range(1, 3650, ErrorMessage = "Los días deben estar entre 1 y 3650.")]
    [Required(ErrorMessage = "Define los días sin movimiento de este producto.")]
    [Display(Name = "Días sin movimiento")]
    public int? NoMovementDaysOverride { get; set; }

    public IReadOnlyList<SelectListItem> Categories { get; set; } = [];
    public IReadOnlyList<string> Brands { get; set; } = [];
    public IReadOnlyList<SelectListItem> ParentProducts { get; set; } = [];
    public List<SelectListItem> AvailableArticleTypes { get; set; } = [];
    public List<SelectListItem> AvailableUnitsOfMeasure { get; set; } = [];
    public List<ProductBranchSettingFormViewModel> BranchSettings { get; set; } = [];
    public List<ProductPurchasePresentationFormViewModel> PurchasePresentations { get; set; } = [];
    public List<ProductUnitConversionFormViewModel> UnitConversions { get; set; } = [];
    public List<CatalogChangeLogDto> ChangeLogs { get; set; } = [];

    public string CurrencySymbol { get; set; } = "Q";
    public int CurrencyDecimals { get; set; } = 2;
}

public sealed class ProductBranchSettingFormViewModel
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    [Range(0, double.MaxValue, ErrorMessage = "El precio por sucursal no puede ser negativo.")]
    public decimal? SalePrice { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El stock mínimo por sucursal no puede ser negativo.")]
    public decimal? MinimumStock { get; set; }
}

public sealed class ProductPurchasePresentationFormViewModel
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public decimal? BaseQuantityPerPackage { get; set; }
    public Guid? PurchaseUnitId { get; set; }
    public string? PurchaseUnitName { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductUnitConversionFormViewModel
{
    public Guid? Id { get; set; }
    public Guid FromUnitId { get; set; }
    public string FromUnitCode { get; set; } = string.Empty;
    public Guid ToUnitId { get; set; }
    public string ToUnitCode { get; set; } = string.Empty;
    public decimal Factor { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductAttributeFormViewModel
{
    public string TypeName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class VariantGroupFormViewModel
{
    public string TypeName { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
}

public sealed class ProductCombinationFormViewModel
{
    public Guid? Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    [Display(Name = "Imagen")]
    public IFormFile? ImageFile { get; set; }
    public string? ImageUrl { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")] public decimal LastCost { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")] public decimal SalePrice { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")] public decimal MinimumStock { get; set; }
    public decimal CurrentStock { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductPriceRuleFormViewModel
{
    public Guid? Id { get; set; }

    [Range(typeof(decimal), "1", "2147483647", ErrorMessage = "La cantidad debe ser un número entero mayor que cero.")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "El precio no puede ser negativo.")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
}
