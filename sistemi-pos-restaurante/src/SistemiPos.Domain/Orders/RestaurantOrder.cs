using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Domain.Orders;

public enum OrderEventType { Confirm = 1, StartPreparation = 2, FinishPreparation = 3, Dispatch = 4, Cancel = 5 }

public sealed class RestaurantOrder : AuditableEntity
{
    private readonly List<RestaurantOrderLine> _lines = [];
    private readonly List<RestaurantOrderEvent> _events = [];
    public Guid TerminalId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid CashSessionId { get; private set; }
    public bool IsConfirmed { get; private set; }
    public uint Version { get; private set; }
    public int Revision { get; private set; }
    public string? CurrencyCode { get; private set; }
    public void SetCurrency(string currencyCode)
    {
        if (IsConfirmed || string.IsNullOrWhiteSpace(currencyCode))
            throw new DomainRuleException("La moneda debe fijarse antes de confirmar el pedido.");
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
    }
    public IReadOnlyCollection<RestaurantOrderLine> Lines => _lines;
    public IReadOnlyCollection<RestaurantOrderEvent> Events => _events;
    private RestaurantOrder() { }

    public RestaurantOrder(PosTerminal terminal, Guid cashSessionId, IEnumerable<RestaurantOrderLine> lines)
    {
        if (!terminal.IsActive || cashSessionId == Guid.Empty)
            throw new DomainRuleException("El pedido requiere terminal y caja activas.");
        TerminalId = terminal.Id;
        BranchId = terminal.BranchId;
        WarehouseId = terminal.WarehouseId;
        CashSessionId = cashSessionId;
        _lines.AddRange(lines);
        if (_lines.Count == 0 || _lines.Count > 200)
            throw new DomainRuleException("El pedido debe tener entre 1 y 200 líneas.");
        foreach (var line in _lines) line.AttachTo(Id);
    }

    public RestaurantOrderLine Line(Guid id) => _lines.SingleOrDefault(x => x.Id == id)
        ?? throw new DomainRuleException("La línea no pertenece al pedido.");
    public void AppendRevision(Guid previousLineId, RestaurantOrderLine replacement, string operationId, string reason, DateTimeOffset at, string user)
    {
        var previous = Line(previousLineId);
        if (!IsConfirmed || previous.StartedQuantity != 0 || previous.CancelledQuantity != 0 || previous.SoldQuantity != 0)
            throw new DomainRuleException("Solo se puede revisar una línea confirmada que no ha iniciado preparación, despacho o cobro.");
        Apply(OrderEventType.Cancel, previous.Id, previous.Quantity, operationId, at, user, reason);
        replacement.SetRevisionOrigin(previous.Id, operationId.Trim());
        replacement.AttachTo(Id);
        _lines.Add(replacement);
        Revision++;
    }
    public void RecordSale(Guid lineId, decimal quantity)
    {
        if (!IsConfirmed) throw new DomainRuleException("Confirma el pedido antes de cobrarlo.");
        Line(lineId).RecordSale(quantity);
        Revision++;
    }

    public void ReplaceDraftLines(IEnumerable<RestaurantOrderLine> lines)
    {
        if (IsConfirmed) throw new DomainRuleException("El histórico de un pedido confirmado no puede sobrescribirse.");
        var values = lines.ToList();
        if (values.Count == 0 || values.Count > 200)
            throw new DomainRuleException("El pedido debe tener entre 1 y 200 líneas.");
        _lines.Clear();
        foreach (var line in values) line.AttachTo(Id);
        _lines.AddRange(values);
        Revision++;
    }

    public bool IsReplay(string operationId, OrderEventType type, Guid? lineId, decimal quantity, string? reason, string? extraPackagingJson = null)
    {
        var previous = _events.SingleOrDefault(x => x.OperationId == operationId.Trim());
        if (previous is null) return false;
        if (previous.Type != type || previous.LineId != lineId || previous.Quantity != quantity
            || previous.Reason != NormalizeReason(reason) || previous.ExtraPackagingJson != extraPackagingJson)
            throw new DomainRuleException("El identificador ya corresponde a otra operación del pedido.");
        return true;
    }

    public void Apply(OrderEventType type, Guid? lineId, decimal quantity, string operationId,
        DateTimeOffset at, string user, string? reason = null, string? extraPackagingJson = null)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Trim().Length > 80)
            throw new DomainRuleException("El identificador de operación es obligatorio y admite hasta 80 caracteres.");
        if (reason?.Trim().Length > 500 || string.IsNullOrWhiteSpace(user) || user.Length > 256)
            throw new DomainRuleException("El usuario es obligatorio y el motivo admite hasta 500 caracteres.");
        if (IsReplay(operationId, type, lineId, quantity, reason, extraPackagingJson)) return;
        if (type == OrderEventType.Confirm)
        {
            if (IsConfirmed || lineId.HasValue || quantity != 0)
                throw new DomainRuleException("El pedido ya está confirmado o la confirmación no es válida.");
            IsConfirmed = true;
        }
        else
        {
            if (!IsConfirmed || !lineId.HasValue)
                throw new DomainRuleException("Confirma el pedido y selecciona una línea antes de continuar.");
            Line(lineId.Value).Apply(type, quantity, reason);
        }
        _events.Add(new RestaurantOrderEvent(Id, type, lineId, quantity, operationId.Trim(), at, user, NormalizeReason(reason), extraPackagingJson));
        Revision++;
    }

    private static string? NormalizeReason(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class RestaurantOrderLine : Entity
{
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal TaxPercent { get; private set; }
    public bool IsTaxExempt { get; private set; }
    public string? RecipeJson { get; private set; }
    public string? ModifiersJson { get; private set; }
    public Guid? ReplacesLineId { get; private set; }
    public string? RevisionOperationId { get; private set; }
    internal void SetRevisionOrigin(Guid lineId, string operationId) { ReplacesLineId = lineId; RevisionOperationId = operationId; }
    public int? RecipeRevision { get; private set; }
    public decimal PreparingQuantity { get; private set; }
    public decimal PreparedQuantity { get; private set; }
    public decimal DispatchedQuantity { get; private set; }
    public decimal CancelledQuantity { get; private set; }
    public decimal CancelledAfterPreparationQuantity { get; private set; }
    public decimal StartedQuantity { get; private set; }
    public decimal? CapturedConsumptionCost { get; private set; }
    public void CaptureConsumptionCost(decimal amount, decimal quantityStartedNow)
    {
        if (amount < 0) throw new DomainRuleException("El costo del consumo no puede ser negativo.");
        if (CapturedConsumptionCost.HasValue) CapturedConsumptionCost += amount;
        else if (StartedQuantity == quantityStartedNow) CapturedConsumptionCost = amount;
        // Old partially prepared lines without an original cost remain unknown.
    }
    public decimal SoldQuantity { get; private set; }
    public decimal AllocatedGrossAmount { get; private set; }
    public decimal SellableQuantity => RequiresPreparation
        ? PreparedQuantity + DispatchedQuantity - SoldQuantity
        : Quantity - CancelledQuantity - SoldQuantity;
    public decimal QuoteGross(decimal quantity)
    {
        RecipeRules.ValidateQuantity(quantity, "La cantidad a cobrar");
        if (decimal.Round(quantity, 2) != quantity || quantity > SellableQuantity)
            throw new DomainRuleException("La cantidad no está preparada o ya fue cobrada/cancelada.");
        return decimal.Round(GrossAmount * (SoldQuantity + quantity) / Quantity, 2) - AllocatedGrossAmount;
    }
    internal void RecordSale(decimal quantity)
    {
        var gross = QuoteGross(quantity);
        SoldQuantity += quantity;
        AllocatedGrossAmount += gross;
    }
    public bool RequiresPreparation => RecipeJson is not null;
    public decimal PendingQuantity => Quantity - StartedQuantity - CancelledQuantity + CancelledAfterPreparationQuantity;
    private RestaurantOrderLine() { }

    internal void AttachTo(Guid orderId)
    {
        if (OrderId != Guid.Empty && OrderId != orderId)
            throw new DomainRuleException("La línea ya pertenece a otro pedido.");
        OrderId = orderId;
    }

    public RestaurantOrderLine(Guid productId, string name, decimal quantity, decimal unitPrice,
        decimal grossAmount, decimal taxPercent, bool taxExempt, string? recipeJson, int? recipeRevision, string? modifiersJson = null)
    {
        RecipeRules.ValidateQuantity(quantity, "La cantidad");
        // Commercial quantities follow the existing sales precision; components use six decimals.
        if (decimal.Round(quantity, 2) != quantity || productId == Guid.Empty || string.IsNullOrWhiteSpace(name)
            || unitPrice < 0 || grossAmount < 0 || taxPercent < 0)
            throw new DomainRuleException("Los datos comerciales de la línea no son válidos.");
        ProductId = productId;
        ProductName = name;
        Quantity = quantity;
        UnitPrice = unitPrice;
        GrossAmount = grossAmount;
        TaxPercent = taxPercent;
        IsTaxExempt = taxExempt;
        RecipeJson = recipeJson;
        ModifiersJson = modifiersJson;
        RecipeRevision = recipeRevision;
    }

    internal void Apply(OrderEventType type, decimal quantity, string? reason)
    {
        RecipeRules.ValidateQuantity(quantity, "La cantidad");
        if (decimal.Round(quantity, 2) != quantity)
            throw new DomainRuleException("La cantidad comercial admite hasta dos decimales.");
        switch (type)
        {
            case OrderEventType.StartPreparation when RequiresPreparation && quantity <= PendingQuantity:
                StartedQuantity += quantity;
                PreparingQuantity += quantity;
                break;
            case OrderEventType.FinishPreparation when RequiresPreparation && quantity <= PreparingQuantity:
                PreparingQuantity -= quantity;
                PreparedQuantity += quantity;
                break;
            case OrderEventType.Dispatch when RequiresPreparation && quantity <= PreparedQuantity:
                PreparedQuantity -= quantity;
                DispatchedQuantity += quantity;
                break;
            case OrderEventType.Dispatch when !RequiresPreparation && quantity <= PendingQuantity:
                StartedQuantity += quantity;
                DispatchedQuantity += quantity;
                break;
            case OrderEventType.Cancel when quantity <= Quantity - CancelledQuantity - Math.Max(DispatchedQuantity, SoldQuantity) && !string.IsNullOrWhiteSpace(reason)
                && reason.Trim().Length <= 500:
                var afterPreparation = Math.Max(0, quantity - PendingQuantity);
                var fromPreparing = Math.Min(afterPreparation, PreparingQuantity);
                PreparingQuantity -= fromPreparing;
                PreparedQuantity -= afterPreparation - fromPreparing;
                CancelledAfterPreparationQuantity += afterPreparation;
                CancelledQuantity += quantity;
                break;
            default:
                throw new DomainRuleException("La transición o cantidad no es válida para el estado de la línea.");
        }
    }
}

public sealed class RestaurantOrderEvent : Entity
{
    public Guid OrderId { get; private set; }
    public Guid? LineId { get; private set; }
    public OrderEventType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public string OperationId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string? ExtraPackagingJson { get; private set; }
    private RestaurantOrderEvent() { }
    internal RestaurantOrderEvent(Guid orderId, OrderEventType type, Guid? lineId, decimal quantity,
        string operationId, DateTimeOffset at, string user, string? reason, string? extraPackagingJson = null)
    {
        OrderId = orderId; Type = type; LineId = lineId; Quantity = quantity;
        OperationId = operationId; OccurredAtUtc = at; UserName = user; Reason = reason;
        ExtraPackagingJson = extraPackagingJson;
    }
}
