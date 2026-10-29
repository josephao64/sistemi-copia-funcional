using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Sales;
using SistemiPos.Infrastructure.Persistence;

namespace SistemiPos.Infrastructure.Sales.PdfJobs;

public sealed class SalePdfProcessor(AppDbContext db, ISaleReceiptService receipts, IDateTimeProvider clock,
    ILogger<SalePdfProcessor>? logger = null)
{
    // Keep the row locked until the artifact is stored. A redelivery cannot generate it twice.
    public async Task ProcessAsync(Guid jobId, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await db.Set<SalePdfJob>().FromSqlInterpolated($"SELECT * FROM \"SalePdfJobs\" WHERE \"Id\" = {jobId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (job is null || job.Status is "Completed" or "Failed") { await tx.CommitAsync(ct); return; }
        if (!await db.Sales.AnyAsync(s => s.Id == job.SaleId
            && db.CashSessions.Any(c => c.Id == s.CashSessionId && c.BranchId == job.BranchId), ct))
            throw new InvalidOperationException("PDF source is outside the recorded branch.");
        job.Complete(await receipts.GeneratePdfAsync(job.SaleId, ct), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger?.LogInformation("PDF job {JobId} completed in {ElapsedMs} ms.", jobId,
            System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
    public async Task<bool> RecordFailureAsync(Guid jobId, int maximumAttempts, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await db.Set<SalePdfJob>().FromSqlInterpolated($"SELECT * FROM \"SalePdfJobs\" WHERE \"Id\" = {jobId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (job is null || job.Status == "Completed") { await tx.CommitAsync(ct); return false; }
        if (job.Status == "Failed") { await tx.CommitAsync(ct); return true; }
        job.FailedAttempt(Math.Clamp(maximumAttempts, 1, 20));
        if (job.Status != "Failed")
        {
            var outbox = await db.Set<SalePdfOutbox>()
                .FromSqlInterpolated($"SELECT * FROM \"SalePdfOutbox\" WHERE \"JobId\" = {jobId} FOR UPDATE")
                .SingleAsync(ct);
            outbox.Retry(clock.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, job.Attempts))));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return job.Status == "Failed";
    }
}
