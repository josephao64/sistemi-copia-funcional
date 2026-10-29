namespace SistemiPos.Application.Suppliers;

public sealed record SaveSupplierRequest(
    string LegalName,
    string Nit,
    string? TradeName,
    string? Phone,
    string? Email,
    string? Address,
    string? ContactName,
    bool IsActive,
    int? DefaultCreditDays = null,
    SistemiPos.Domain.Purchases.PurchaseTermsType DefaultTerms = SistemiPos.Domain.Purchases.PurchaseTermsType.Cash,
    string? ContactPosition = null,
    string? ContactPhone = null,
    string? Notes = null);
