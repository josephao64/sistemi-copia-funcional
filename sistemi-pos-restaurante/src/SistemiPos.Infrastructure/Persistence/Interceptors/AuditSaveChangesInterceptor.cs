using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SistemiPos.Application.Abstractions;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Common;

namespace SistemiPos.Infrastructure.Persistence.Interceptors;

internal sealed class AuditSaveChangesInterceptor(
    ICurrentUserAccessor currentUserAccessor,
    IDateTimeProvider dateTimeProvider,
    IServiceProvider? services = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
    private void Capture(DbContext? db)
    {
        if (db is null) return;
        // Resolve only while saving: the HTTP context accessor depends on repositories using this DbContext.
        var operationContext = services?.GetService<IOperationContextAccessor>();
        var now = dateTimeProvider.UtcNow;
        var user = currentUserAccessor.UserName;
        var logs = new List<AuditLog>();
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged) continue;
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added) { auditable.CreatedAtUtc = now; auditable.CreatedBy = user; }
                auditable.ModifiedAtUtc = now; auditable.ModifiedBy = user;
            }
            if (entry.Entity is not Entity entity) continue;
            object? Value(string name) => entry.Metadata.FindProperty(name) is null ? null : entry.Property(name).CurrentValue;
            Guid? Id(string name) => Value(name) is Guid id && id != Guid.Empty ? id : null;
            var changes = entry.Properties
                .Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Where(p => p.Metadata.Name is not ("Version" or "ModifiedAtUtc" or "ModifiedBy"))
                .ToDictionary(p => p.Metadata.Name, p => new
                {
                    Original = Sensitive(p.Metadata.Name) ? "[PROTEGIDO]" : entry.State == EntityState.Added ? null : p.OriginalValue,
                    Current = Sensitive(p.Metadata.Name) ? "[PROTEGIDO]" : entry.State == EntityState.Deleted ? null : p.CurrentValue
                });
            logs.Add(new AuditLog {
                Module = entry.Entity.GetType().Namespace?.Split('.').Last() ?? "Base",
                Action = entry.State.ToString(), RecordType = entry.Entity.GetType().Name, RecordId = entity.Id.ToString(),
                DocumentNumber = Value("Number")?.ToString(),
                UserName = user, OccurredAtUtc = now, ChangesJson = JsonSerializer.Serialize(changes),
                BusinessGroupId = Id("BusinessGroupId") ?? operationContext?.CurrentBusinessGroupId,
                CompanyId = Id("CompanyId") ?? operationContext?.CurrentCompanyId,
                BranchId = Id("BranchId") ?? operationContext?.CurrentBranchId,
                WarehouseId = Id("WarehouseId") ?? operationContext?.CurrentWarehouseId,
                TerminalId = Id("TerminalId")
            });
        }
        if (logs.Count > 0) db.Set<AuditLog>().AddRange(logs);
    }
    internal static bool Sensitive(string name) => new[] { "password", "protected", "secret", "token", "credential", "connectionstring", "securitystamp" }
        .Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase));
}
