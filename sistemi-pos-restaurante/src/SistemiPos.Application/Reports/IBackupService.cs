namespace SistemiPos.Application.Reports;

public sealed record BackupInfo(string File, DateTimeOffset CreatedAtUtc, long Bytes, string Sha256,
    bool Verified, string? Error = null, IReadOnlyDictionary<string,long>? Counts = null);
public sealed record BackupStatus(bool Configured, bool AutomaticEnabled, int IntervalHours, int RetentionDays,
    int? RpoMinutes, int? RtoMinutes, IReadOnlyList<BackupInfo> Files);
public interface IBackupService
{
    Task<BackupStatus> StatusAsync(CancellationToken ct = default);
    Task<BackupInfo> CreateAsync(CancellationToken ct = default);
}
