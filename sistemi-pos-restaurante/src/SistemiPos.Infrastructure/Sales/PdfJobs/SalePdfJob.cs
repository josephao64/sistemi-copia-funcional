using SistemiPos.Domain.Common;

namespace SistemiPos.Infrastructure.Sales.PdfJobs;

public sealed class SalePdfJob : Entity
{
    public Guid SaleId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string RequestKey { get; private set; } = "";
    public string SaleNumber { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string Status { get; private set; } = "Pending";
    public int Attempts { get; private set; }
    public byte[]? Content { get; private set; }
    private SalePdfJob() { }
    public SalePdfJob(Guid saleId, Guid branchId, Guid userId, string key, string number, DateTimeOffset now)
    {
        SaleId = saleId; BranchId = branchId; RequestedByUserId = userId; RequestKey = key;
        SaleNumber = number; CreatedAtUtc = now;
    }
    public void Complete(byte[] content, DateTimeOffset now)
    {
        if (Status == "Completed") return;
        Content = content; CompletedAtUtc = now; Status = "Completed"; Attempts++;
    }
    public void FailedAttempt(int maximumAttempts)
    {
        if (Status == "Completed") return;
        Attempts++;
        Status = Attempts >= maximumAttempts ? "Failed" : "Pending";
    }
}

public sealed class SalePdfOutbox : Entity
{
    public Guid JobId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset AvailableAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    private SalePdfOutbox() { }
    public SalePdfOutbox(Guid jobId, DateTimeOffset now) { JobId = jobId; CreatedAtUtc = AvailableAtUtc = now; }
    public void Published(DateTimeOffset now) => PublishedAtUtc = now;
    public void Retry(DateTimeOffset when) { AvailableAtUtc = when; PublishedAtUtc = null; }
}
