using System.ComponentModel.DataAnnotations;
using SistemiPos.Domain.Configuration;

namespace SistemiPos.Web.Models.Settings;

public sealed class SystemSettingFormViewModel
{
    [Required(ErrorMessage = "Seleccione una moneda.")]
    [Display(Name = "Moneda")]
    public Guid? CurrencyId { get; set; }
    [Required(ErrorMessage = "El nombre del negocio es requerido.")]
    [Display(Name = "Nombre del negocio")]
    public string BusinessName { get; set; } = string.Empty;

    [Display(Name = "Razón social")]
    public string? BusinessLegalName { get; set; }

    [Display(Name = "NIT")]
    public string? BusinessNit { get; set; }

    [Display(Name = "Dirección")]
    public string? BusinessAddress { get; set; }

    [Display(Name = "Teléfono")]
    public string? BusinessPhone { get; set; }

    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [Display(Name = "Correo de la empresa")]
    public string? BusinessEmail { get; set; }

    [Display(Name = "Logo URL")]
    public string? BusinessLogoUrl { get; set; }

    [Required(ErrorMessage = "El código de moneda es requerido.")]
    [Display(Name = "Código de moneda")]
    public string CurrencyCode { get; set; } = "GTQ";

    [Required(ErrorMessage = "El nombre de moneda es requerido.")]
    [Display(Name = "Nombre de moneda")]
    public string CurrencyName { get; set; } = "Quetzal";

    [Required(ErrorMessage = "El símbolo de moneda es requerido.")]
    [Display(Name = "Símbolo")]
    public string CurrencySymbol { get; set; } = "Q";

    [Range(0, 6, ErrorMessage = "Los decimales deben estar entre 0 y 6.")]
    [Display(Name = "Decimales")]
    public int CurrencyDecimals { get; set; } = 2;

    [Required(ErrorMessage = "El separador decimal es requerido.")]
    [Display(Name = "Separador decimal")]
    public string DecimalSeparator { get; set; } = ".";

    [Required(ErrorMessage = "El separador de miles es requerido.")]
    [Display(Name = "Separador de miles")]
    public string ThousandsSeparator { get; set; } = ",";

    [Display(Name = "Posición del símbolo")]
    public CurrencySymbolPosition CurrencySymbolPosition { get; set; } = CurrencySymbolPosition.Before;

    [Range(0, 100, ErrorMessage = "El impuesto no puede ser negativo.")]
    [Display(Name = "Impuesto %")]
    public decimal TaxPercent { get; set; } = 12m;

    [Display(Name = "Mostrar impuesto en comprobante")]
    public bool ShowTaxOnReceipt { get; set; } = true;

    [Display(Name = "Leyenda al pie")]
    public string? ReceiptFooter { get; set; }

    [Display(Name = "Efectivo habilitado")]
    public bool CashPaymentsEnabled { get; set; } = true;

    [Display(Name = "Transferencia habilitada")]
    public bool TransferPaymentsEnabled { get; set; } = true;

    [Display(Name = "POS habilitado")]
    public bool PosPaymentsEnabled { get; set; } = true;

    [Display(Name = "Crédito habilitado")]
    public bool CreditPaymentsEnabled { get; set; } = true;

    [Display(Name = "Habilitar descuentos")]
    public bool DiscountsEnabled { get; set; }

    [Range(0, 100, ErrorMessage = "El porcentaje máximo no puede ser negativo.")]
    [Display(Name = "Máximo general de descuento %")]
    public decimal DefaultMaxDiscountPercent { get; set; }

    [Display(Name = "Proveedor de consulta NIT")]
    public NitLookupProviderType NitLookupProvider { get; set; } = NitLookupProviderType.None;

    [Range(1, 3650, ErrorMessage = "Los días deben estar entre 1 y 3650.")]
    [Display(Name = "Días para considerar producto sin movimiento")]
    public int ProductsWithoutMovementDays { get; set; } = 30;

    [Display(Name = "Nombre del establecimiento")]
    public string? EstablishmentName { get; set; }

    [Display(Name = "Código / número de establecimiento")]
    public string? EstablishmentCode { get; set; }

    [Display(Name = "Activar emisión FEL")]
    public bool FelEnabled { get; set; }

    [Display(Name = "Ambiente")]
    public FelEnvironmentType FelEnvironment { get; set; } = FelEnvironmentType.Testing;

    [Display(Name = "Tipo de documento predeterminado")]
    public string DefaultFelDocumentType { get; set; } = "FACT";

    [Display(Name = "Frases tributarias / configuración fiscal")]
    public string? FelTaxPhrases { get; set; }

    [Display(Name = "Usuario de API")]
    public string? InfileApiUser { get; set; }

    [Display(Name = "Llave de API")]
    public string? InfileApiKey { get; set; }

    [Display(Name = "Usuario de firma")]
    public string? InfileSignatureUser { get; set; }

    [Display(Name = "Llave de firma")]
    public string? InfileSignatureKey { get; set; }

    public bool HasInfileApiUser { get; set; }
    public bool HasInfileApiKey { get; set; }
    public bool HasInfileSignatureUser { get; set; }
    public bool HasInfileSignatureKey { get; set; }
}
