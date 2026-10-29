using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class FelCertification : AuditableEntity
{
    public Guid SaleId { get; private set; }
    public string Provider { get; private set; } = "INFILE";
    public string AuthorizationNumber { get; private set; } = string.Empty;
    public string Series { get; private set; } = string.Empty;
    public string DocumentNumber { get; private set; } = string.Empty;
    public DateTimeOffset CertifiedAtUtc { get; private set; }
    public string CertifierName { get; private set; } = string.Empty;
    public string? CertifierNit { get; private set; }
    public string? QrCode { get; private set; }
    public string? AccessNumber { get; private set; }
    public string? CertifiedDocumentXml { get; private set; }
    public string? CertificationResponseJson { get; private set; }

    private FelCertification() { }

    public FelCertification(
        Guid saleId,
        string authorizationNumber,
        string series,
        string documentNumber,
        DateTimeOffset certifiedAtUtc,
        string certifierName,
        string? certifierNit,
        string? qrCode,
        string? accessNumber,
        string? certifiedDocumentXml,
        string? certificationResponseJson)
    {
        if (saleId == Guid.Empty) throw new DomainRuleException("La venta certificada es requerida.");
        if (string.IsNullOrWhiteSpace(authorizationNumber) || string.IsNullOrWhiteSpace(series) || string.IsNullOrWhiteSpace(documentNumber))
            throw new DomainRuleException("La autorización, serie y número DTE deben provenir de la certificación FEL.");
        if (string.IsNullOrWhiteSpace(certifierName)) throw new DomainRuleException("Los datos del certificador son requeridos.");

        SaleId = saleId;
        AuthorizationNumber = authorizationNumber.Trim();
        Series = series.Trim();
        DocumentNumber = documentNumber.Trim();
        CertifiedAtUtc = certifiedAtUtc;
        CertifierName = certifierName.Trim();
        CertifierNit = string.IsNullOrWhiteSpace(certifierNit) ? null : certifierNit.Trim();
        QrCode = string.IsNullOrWhiteSpace(qrCode) ? null : qrCode.Trim();
        AccessNumber = string.IsNullOrWhiteSpace(accessNumber) ? null : accessNumber.Trim();
        CertifiedDocumentXml = string.IsNullOrWhiteSpace(certifiedDocumentXml) ? null : certifiedDocumentXml;
        CertificationResponseJson = string.IsNullOrWhiteSpace(certificationResponseJson) ? null : certificationResponseJson;
    }
}
