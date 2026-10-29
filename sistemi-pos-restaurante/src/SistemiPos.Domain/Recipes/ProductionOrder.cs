using System.Text.Json;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Recipes;

public enum ProductionStatus { Draft = 1, Confirmed = 2 }

public sealed class ProductionOrder : AuditableEntity
{
    public Guid OperationId { get; private set; }
    public Guid RecipeVersionId { get; private set; }
    public RecipeVersion RecipeVersion { get; set; } = null!;
    public Guid OriginWarehouseId { get; private set; }
    public Guid DestinationWarehouseId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string LotNumber { get; private set; } = string.Empty;
    public DateOnly? ExpiryDate { get; private set; }
    public decimal PlannedQuantity { get; private set; }
    public decimal? ActualQuantity { get; private set; }
    public decimal WasteQuantity { get; private set; }
    public string? VarianceReason { get; private set; }
    public DateTimeOffset ScheduledAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public string ResponsibleUser { get; private set; } = string.Empty;
    public string? ConfirmedBy { get; private set; }
    public ProductionStatus Status { get; private set; } = ProductionStatus.Draft;
    public string ConsumptionJson { get; private set; } = "[]";
    public decimal TotalCost { get; private set; }
    public decimal? UnitCost { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<RecipeConsumption> ReadConsumption() => JsonSerializer.Deserialize<List<RecipeConsumption>>(ConsumptionJson)!;
    private ProductionOrder() { }
    public ProductionOrder(Guid operationId, Guid recipeVersionId, Guid origin, Guid destination,
        decimal plannedQuantity, string lotNumber, DateOnly? expiryDate, DateTimeOffset scheduledAtUtc, string user)
    {
        if (operationId == Guid.Empty || recipeVersionId == Guid.Empty || origin == Guid.Empty || destination == Guid.Empty)
            throw new DomainRuleException("La receta, operación y bodegas son requeridas.");
        RecipeRules.ValidateQuantity(plannedQuantity, "La cantidad planificada");
        if (string.IsNullOrWhiteSpace(lotNumber) || lotNumber.Trim().Length > 100)
            throw new DomainRuleException("Indica un lote de hasta 100 caracteres.");
        OperationId = operationId; RecipeVersionId = recipeVersionId; OriginWarehouseId = origin;
        DestinationWarehouseId = destination; PlannedQuantity = plannedQuantity; LotNumber = lotNumber.Trim();
        ExpiryDate = expiryDate; ScheduledAtUtc = scheduledAtUtc; ResponsibleUser = user;
        Number = $"PRO-{Id:N}";
    }
    public void Confirm(decimal actual, string? reason, IReadOnlyList<RecipeConsumption> consumption,
        DateTimeOffset now, string user, int precision)
    {
        if (Status != ProductionStatus.Draft) throw new DomainRuleException("La producción ya fue confirmada.");
        if (actual < 0 || actual > 999999999999.999999m || decimal.Round(actual, precision) != actual)
            throw new DomainRuleException("El rendimiento real no respeta la precisión de la unidad.");
        if (actual != PlannedQuantity && string.IsNullOrWhiteSpace(reason))
            throw new DomainRuleException("Indica el motivo de la diferencia entre rendimiento teórico y real.");
        if (reason?.Trim().Length > 500) throw new DomainRuleException("El motivo admite hasta 500 caracteres.");
        ActualQuantity = actual; WasteQuantity = Math.Max(0, PlannedQuantity - actual);
        VarianceReason = reason?.Trim(); ConsumptionJson = JsonSerializer.Serialize(consumption);
        TotalCost = decimal.Round(consumption.Sum(x => x.Quantity * x.UnitCost), 6);
        UnitCost = actual > 0 ? decimal.Round(TotalCost / actual, 6) : null;
        ConfirmedAtUtc = now; ConfirmedBy = user; Status = ProductionStatus.Confirmed;
    }
}

// Baseline at deployment: older dates with no purchase history remain explicitly unknown.
public sealed class ProductCostBaseline
{
    public Guid ProductId { get; set; }
    public DateTimeOffset EffectiveAtUtc { get; set; }
    public decimal UnitCost { get; set; }
}
