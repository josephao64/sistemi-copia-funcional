using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Sales;

public sealed class UnconfiguredFiscalProvider : IFiscalProvider
{
    public bool IsConfigured => false;
    public Task<FiscalProviderResult> CertifyAsync(string immutablePayload, string idempotencyKey, CancellationToken ct) =>
        throw new DomainRuleException("Primero debe contratarse y configurar el certificador FEL.");
}

public sealed class FiscalService(AppDbContext db, IOperationContextAccessor context, ICurrentUserAccessor user,
    IOperationAuthorizationService auth, IFiscalProvider provider) : IFiscalService
{
    private async Task<Guid[]> Scope(CancellationToken ct)
    {
        if (!user.UserId.HasValue || !await auth.HasPermissionAsync(user.UserName, PermissionCodes.FiscalManage, ct))
            throw new DomainRuleException("No tienes permiso para gestionar documentos fiscales.");
        return (await context.GetAuthorizedBranchesAsync(ct)).Where(x => x.IsActive).Select(x => x.Id).ToArray();
    }
    public async Task<IReadOnlyList<FiscalCandidate>> ListAsync(CancellationToken ct = default)
    {
        var ids = await Scope(ct);
        var query = from sale in db.Sales.AsNoTracking()
            join cash in db.CashSessions on sale.CashSessionId equals cash.Id
            join branch in db.Branches on cash.BranchId equals branch.Id
            where ids.Contains(branch.Id) && sale.Status != SaleStatus.Cancelled
            orderby sale.SoldAtUtc descending
            select new { sale, branch.Name, Submission = db.Set<FiscalSubmission>().FirstOrDefault(x => x.SaleId == sale.Id),
                Certificate = db.FelCertifications.FirstOrDefault(x => x.SaleId == sale.Id) };
        var rows = await query.Take(200).ToListAsync(ct);
        return rows.Select(x => new FiscalCandidate(x.sale.Id, x.sale.Number, x.Name, x.sale.CurrencyCode, x.sale.Total,
            x.Submission?.Status, x.Certificate?.AuthorizationNumber, x.Submission?.Message)).ToList();
    }
    public async Task PrepareAsync(Guid saleId, CancellationToken ct = default)
    {
        var ids = await Scope(ct);
        var sale = await db.Sales.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == saleId, ct)
            ?? throw new DomainRuleException("Venta no encontrada.");
        var cash = await db.CashSessions.SingleAsync(x => x.Id == sale.CashSessionId, ct);
        if (!cash.BranchId.HasValue || !ids.Contains(cash.BranchId.Value))
            throw new DomainRuleException("La venta está fuera de tu alcance.");
        if (sale.Status != SaleStatus.Confirmed) throw new DomainRuleException("Revisa anulaciones y devoluciones antes de preparar la certificación.");
        if (await db.FelCertifications.AnyAsync(x => x.SaleId == saleId, ct)) return;
        var branch = await db.Branches.SingleAsync(x => x.Id == cash.BranchId, ct);
        var settings = await db.CompanySettings.SingleOrDefaultAsync(x => x.CompanyId == branch.CompanyId, ct);
        var submission = await db.Set<FiscalSubmission>().SingleOrDefaultAsync(x => x.SaleId == saleId, ct);
        if (submission is null)
        {
            var payload = JsonSerializer.Serialize(new { sale.Id, sale.Number, CompanyId = branch.CompanyId, BranchId = branch.Id,
                EmitterNit = settings?.BusinessNit, Establishment = branch.EstablishmentCode,
                ReceiverNit = sale.CustomerNitSnapshot, ReceiverName = sale.CustomerNameSnapshot,
                ReceiverAddress = sale.CustomerAddressSnapshot, sale.CurrencyCode, sale.SoldAtUtc, sale.Total, sale.TaxTotal,
                Items = sale.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxableBaseAmount, i.TaxAmount, i.Subtotal }) });
            submission = new(sale.Id, branch.CompanyId, branch.Id, payload); db.Add(submission);
        }
        if (!provider.IsConfigured || settings is null || !settings.FelEnabled)
        {
            submission.Complete(FiscalSubmissionStatus.PendingConfiguration, "Pendiente de contrato, configuración fiscal por empresa y conector del certificador.");
            await db.SaveChangesAsync(ct); return;
        }
        submission.Begin();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); throw new DomainRuleException("Otra operación ya preparó esta venta. Recarga su estado."); }
        // The sent state is durable before any external call. Timeouts never trigger blind resubmission.
        try
        {
            var response = await provider.CertifyAsync(submission.PayloadJson, submission.IdempotencyKey, ct);
            if (!response.Accepted) submission.Complete(FiscalSubmissionStatus.Rejected, response.Message ?? "Rechazado por el certificador.");
            else if (!Guid.TryParse(response.Authorization, out _) || string.IsNullOrWhiteSpace(response.Series)
                || string.IsNullOrWhiteSpace(response.Number) || !response.CertifiedAtUtc.HasValue
                || string.IsNullOrWhiteSpace(response.CertifierName) || string.IsNullOrWhiteSpace(response.CertifiedXml))
                submission.Complete(FiscalSubmissionStatus.ReconciliationRequired, "La respuesta no contiene todos los datos fiscales. Conciliar con el certificador.");
            else
            {
                db.FelCertifications.Add(new FelCertification(saleId, response.Authorization!, response.Series!, response.Number!,
                    response.CertifiedAtUtc.Value, response.CertifierName!, response.CertifierNit, null, null,
                    response.CertifiedXml, null));
                submission.Complete(FiscalSubmissionStatus.Certified);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            db.ChangeTracker.Clear();
            var persisted=await db.Set<FiscalSubmission>().SingleAsync(x=>x.SaleId==saleId,CancellationToken.None);
            if(persisted.Status==FiscalSubmissionStatus.Submitting)
            {
                persisted.Complete(FiscalSubmissionStatus.ReconciliationRequired, "Resultado externo incierto. Verificar el documento con el certificador antes de reintentar.");
                await db.SaveChangesAsync(CancellationToken.None);
            }
            throw new DomainRuleException("No se pudo confirmar el resultado fiscal. El documento requiere conciliación.");
        }
    }
}
