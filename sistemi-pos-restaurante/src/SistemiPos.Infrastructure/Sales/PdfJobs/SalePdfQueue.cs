using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Sales;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Sales.PdfJobs;

public sealed class SalePdfQueue(AppDbContext db, ICurrentUserAccessor user, IOperationContextAccessor context,
    IOperationAuthorizationService authorization, IDateTimeProvider clock) : ISalePdfQueue
{
    private async Task<(Guid User, Guid Branch)> Scope(CancellationToken ct)
    {
        if (user.UserId is not Guid actor || context.CurrentBranchId is not Guid branch
            || !await authorization.HasPermissionAsync(user.UserName, PermissionCodes.SalesView, ct))
            throw new DomainRuleException("No tienes autorización para solicitar este comprobante.");
        return (actor, branch);
    }
    public async Task<SalePdfJobDto> RequestAsync(Guid saleId, string requestKey, CancellationToken ct = default)
    {
        if (!Guid.TryParse(requestKey, out var key)) throw new DomainRuleException("La solicitud del PDF no es válida.");
        var scope = await Scope(ct);
        var number = await db.Sales.AsNoTracking().Where(s => s.Id == saleId
            && db.CashSessions.Any(c => c.Id == s.CashSessionId && c.BranchId == scope.Branch))
            .Select(s => s.Number).SingleOrDefaultAsync(ct)
            ?? throw new DomainRuleException("La venta no está disponible en tu sucursal.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var requestScope = $"sale-pdf:{scope.User}:{scope.Branch}:{saleId}:{key:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({requestScope}, 0))", ct);
        var existing = await db.Set<SalePdfJob>().Where(x => x.SaleId == saleId && x.BranchId == scope.Branch
            && x.RequestedByUserId == scope.User && x.RequestKey == key.ToString("N"))
            .Select(x => new SalePdfJobDto(x.Id, x.SaleId, x.SaleNumber, x.Status, x.Attempts)).SingleOrDefaultAsync(ct);
        if (existing is not null) { await tx.CommitAsync(ct); return existing; }
        var job = new SalePdfJob(saleId, scope.Branch, scope.User, key.ToString("N"), number, clock.UtcNow);
        db.Add(job);
        db.Add(new SalePdfOutbox(job.Id, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(job.Id, saleId, number, job.Status, job.Attempts);
    }
    private IQueryable<SalePdfJob> Visible(Guid actor, Guid branch) => db.Set<SalePdfJob>().AsNoTracking()
        .Where(x => x.RequestedByUserId == actor && x.BranchId == branch
            && db.Sales.Any(s => s.Id == x.SaleId && db.CashSessions.Any(c => c.Id == s.CashSessionId && c.BranchId == branch)));
    public async Task<SalePdfJobDto?> GetAsync(Guid jobId, CancellationToken ct = default)
    {
        var scope = await Scope(ct);
        return await Visible(scope.User, scope.Branch).Where(x => x.Id == jobId)
            .Select(x => new SalePdfJobDto(x.Id, x.SaleId, x.SaleNumber, x.Status, x.Attempts)).SingleOrDefaultAsync(ct);
    }
    public async Task<SalePdfDownload?> DownloadAsync(Guid jobId, CancellationToken ct = default)
    {
        var scope = await Scope(ct);
        return await Visible(scope.User, scope.Branch).Where(x => x.Id == jobId && x.Status == "Completed")
            .Select(x => new SalePdfDownload(x.SaleNumber, x.Content!)).SingleOrDefaultAsync(ct);
    }
}
