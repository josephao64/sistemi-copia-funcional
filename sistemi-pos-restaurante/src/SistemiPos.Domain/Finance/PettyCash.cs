using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Finance;

public enum PettyCashCycleStatus { Open = 1, Closed = 2 }

public sealed class PettyCashFund : AuditableEntity
{
    private readonly List<PettyCashMovement> _movements = [];
    public string Name { get; private set; } = string.Empty;
    public Guid ResponsibleUserId { get; private set; }
    public string ResponsibleUserName { get; private set; } = string.Empty;
    public decimal FundAmount { get; private set; }
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? CurrentCycleId { get; private set; }
    public DateTimeOffset? OpenedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public IReadOnlyCollection<PettyCashMovement> Movements => _movements;
    private PettyCashFund() { }
    public PettyCashFund(string name, Guid responsibleUserId, string responsibleUserName) => UpdateAssignment(name, responsibleUserId, responsibleUserName, true);
    [Obsolete("Use the assigned petty-cash constructor.")]
    public PettyCashFund(decimal initialFund, string userName, DateTimeOffset at) { Name = "Caja chica"; ResponsibleUserName = userName; IsActive = true; FundAmount = decimal.Round(initialFund, 2); Balance = FundAmount; OpenedAtUtc = at; if (initialFund > 0) _movements.Add(new PettyCashMovement(PettyCashMovementType.Replenishment, initialFund, "Fondo inicial", null, null, null, null, userName, at)); }
    public void UpdateAssignment(string name, Guid responsibleUserId, string responsibleUserName, bool isActive) { if (string.IsNullOrWhiteSpace(name) || responsibleUserId == Guid.Empty || string.IsNullOrWhiteSpace(responsibleUserName)) throw new DomainRuleException("El nombre y usuario responsable de la Caja chica son requeridos."); Name = NameNormalizer.Normalize(name); ResponsibleUserId = responsibleUserId; ResponsibleUserName = responsibleUserName.Trim(); IsActive = isActive; }
    public PettyCashCycle Open(decimal initialBalance, Guid? userId, string userName, DateTimeOffset at) { EnsureResponsible(userId, userName); if (!IsActive) throw new DomainRuleException("La Caja chica está inactiva."); if (CurrentCycleId is not null) throw new DomainRuleException("La Caja chica ya está abierta."); if (initialBalance < 0) throw new DomainRuleException("El saldo inicial no puede ser negativo."); var cycle = new PettyCashCycle(Id, initialBalance, userName, at); CurrentCycleId = cycle.Id; OpenedAtUtc = at; ClosedAtUtc = null; FundAmount = decimal.Round(initialBalance, 2); Balance = FundAmount; return cycle; }
    public PettyCashMovement AddMovement(PettyCashMovement movement) { EnsureResponsible(null, movement.UserName); if (!IsActive || CurrentCycleId is null) throw new DomainRuleException("Debe abrir la Caja chica antes de registrar movimientos."); if (movement.CycleId != CurrentCycleId) throw new DomainRuleException("El movimiento no pertenece al ciclo actual."); var delta = movement.Type == PettyCashMovementType.Replenishment ? movement.Amount : -movement.Amount; if (Balance + delta < 0) throw new DomainRuleException("Saldo insuficiente en Caja chica."); Balance = decimal.Round(Balance + delta, 2); _movements.Add(movement); return movement; }
    public void Close(PettyCashCycle cycle, decimal countedCash, Guid? userId, string userName, DateTimeOffset at, string? observation) { EnsureResponsible(userId, userName); if (CurrentCycleId is null || cycle.Id != CurrentCycleId) throw new DomainRuleException("No existe un ciclo abierto para cerrar."); cycle.Close(Balance, countedCash, userName, at, observation); CurrentCycleId = null; ClosedAtUtc = at; Balance = decimal.Round(countedCash, 2); }
    public void CancelMovement(Guid movementId, string reason, string userName, string authorizedBy, DateTimeOffset at) { EnsureResponsible(null, userName); var movement = _movements.SingleOrDefault(x => x.Id == movementId && x.CycleId == CurrentCycleId) ?? throw new DomainRuleException("Movimiento del ciclo actual no encontrado."); if (movement.IsCancelled) throw new DomainRuleException("El movimiento ya fue anulado."); var reversal = movement.Type == PettyCashMovementType.Replenishment ? -movement.Amount : movement.Amount; if (Balance + reversal < 0) throw new DomainRuleException("La anulación dejaría un saldo negativo."); Balance = decimal.Round(Balance + reversal, 2); movement.Cancel(reason, userName, authorizedBy, at); }
    public void EnsureResponsible(Guid? userId, string userName) { var idMismatch = ResponsibleUserId != Guid.Empty && userId.HasValue && userId != ResponsibleUserId; if (idMismatch || !string.Equals(ResponsibleUserName, userName, StringComparison.OrdinalIgnoreCase)) throw new DomainRuleException("Solo el usuario responsable puede operar esta Caja chica."); }
}

public sealed class PettyCashCycle : AuditableEntity
{
    public Guid PettyCashFundId { get; private set; } public decimal InitialBalance { get; private set; } public decimal? ExpectedBalance { get; private set; } public decimal? CountedBalance { get; private set; } public decimal? Difference { get; private set; } public PettyCashCycleStatus Status { get; private set; } = PettyCashCycleStatus.Open; public DateTimeOffset OpenedAtUtc { get; private set; } public DateTimeOffset? ClosedAtUtc { get; private set; } public string OpenedByUserName { get; private set; } = string.Empty; public string? ClosedByUserName { get; private set; } public string? Observation { get; private set; }
    private PettyCashCycle() { }
    public PettyCashCycle(Guid fundId, decimal initialBalance, string userName, DateTimeOffset at) { PettyCashFundId = fundId; InitialBalance = decimal.Round(initialBalance, 2); OpenedByUserName = userName; OpenedAtUtc = at; }
    public void Close(decimal expected, decimal counted, string userName, DateTimeOffset at, string? observation) { if (Status == PettyCashCycleStatus.Closed || counted < 0) throw new DomainRuleException("El cierre de Caja chica no es válido."); ExpectedBalance = decimal.Round(expected, 2); CountedBalance = decimal.Round(counted, 2); Difference = decimal.Round(counted - expected, 2); Status = PettyCashCycleStatus.Closed; ClosedAtUtc = at; ClosedByUserName = userName; Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim(); }
}

public sealed class PettyCashCategory : AuditableEntity
{
    public string Name { get; private set; } = string.Empty; public bool IsActive { get; private set; } = true; private PettyCashCategory() { } public PettyCashCategory(string name) { if (string.IsNullOrWhiteSpace(name)) throw new DomainRuleException("El nombre de categoría es requerido."); Name = NameNormalizer.Normalize(name); }
}

public sealed class PettyCashMovement : AuditableEntity
{
    public Guid PettyCashFundId { get; set; } public Guid? CycleId { get; private set; } public Guid? CategoryId { get; private set; } public PettyCashMovementType Type { get; private set; } public decimal Amount { get; private set; } public string Concept { get; private set; } = string.Empty; public string? Category { get; private set; } public string? Document { get; private set; } public string? Beneficiary { get; private set; } public string? Observation { get; private set; } public string UserName { get; private set; } = string.Empty; public DateTimeOffset OccurredAtUtc { get; private set; } public string? AuthorizedByUserName { get; private set; } public string? SourceType { get; private set; } public Guid? SourceId { get; private set; } public bool IsCancelled { get; private set; } public DateTimeOffset? CancelledAtUtc { get; private set; } public string? CancelledByUserName { get; private set; } public string? CancellationAuthorizedByUserName { get; private set; } public string? CancellationReason { get; private set; }
    private PettyCashMovement() { }
    public PettyCashMovement(PettyCashMovementType type, decimal amount, string concept, string? category, string? document, string? beneficiary, string? observation, string userName, DateTimeOffset at, string? authorizedBy = null, string? sourceType = null, Guid? sourceId = null, Guid? cycleId = null, Guid? categoryId = null) { if (amount <= 0 || string.IsNullOrWhiteSpace(concept)) throw new DomainRuleException("El monto y concepto son requeridos."); Type = type; Amount = decimal.Round(amount, 2); Concept = concept.Trim(); Category = N(category); Document = N(document); Beneficiary = N(beneficiary); Observation = N(observation); UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim(); OccurredAtUtc = at; AuthorizedByUserName = N(authorizedBy); SourceType = N(sourceType); SourceId = sourceId; CycleId = cycleId; CategoryId = categoryId; }
    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public void Cancel(string reason, string userName, string authorizedBy, DateTimeOffset at) { if (string.IsNullOrWhiteSpace(reason)) throw new DomainRuleException("El motivo de anulación es requerido."); IsCancelled = true; CancellationReason = reason.Trim(); CancelledByUserName = userName.Trim(); CancellationAuthorizedByUserName = authorizedBy.Trim(); CancelledAtUtc = at; }
}

public sealed class PettyCashSettlement : AuditableEntity
{
    public Guid PettyCashFundId { get; private set; } public decimal ExpectedBalance { get; private set; } public decimal CountedCash { get; private set; } public decimal Difference { get; private set; } public string UserName { get; private set; } = string.Empty; public DateTimeOffset SettledAtUtc { get; private set; } public string? Observation { get; private set; }
    private PettyCashSettlement() { }
    public PettyCashSettlement(Guid fundId, decimal expected, decimal counted, string userName, DateTimeOffset at, string? observation) { if (fundId == Guid.Empty || expected < 0 || counted < 0) throw new DomainRuleException("La liquidación no es válida."); PettyCashFundId = fundId; ExpectedBalance = decimal.Round(expected, 2); CountedCash = decimal.Round(counted, 2); Difference = decimal.Round(counted - expected, 2); UserName = string.IsNullOrWhiteSpace(userName) ? "system" : userName.Trim(); SettledAtUtc = at; Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim(); }
}
