using System.Text.Json;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Recipes;

namespace SistemiPos.Domain.Restaurant;

public enum RestaurantOrderStatus { Draft = 1, Submitted, InPreparation, Ready, OutForDelivery, Delivered, Cancelled }
public enum KitchenLineStatus { Queued = 1, Preparing, Ready, HandedOff, Cancelled, Split }
public enum ReservationStatus { Active = 1, Consumed, Released }
public sealed record OrderLineSnapshot(Guid ProductId, string ProductName, decimal Quantity, decimal Gross,
    decimal Discount, decimal TaxPercent, Guid StationId, string StationName, Guid? RecipeVersionId,
    string Options, List<RecipeConsumption> Consumption, Guid? ComboId = null, string? ComboName = null,
    Guid? PromotionId = null, string? PromotionName = null, int ComboSequence = 0,
    DateTimeOffset? PromotionFromUtc = null, DateTimeOffset? PromotionToUtc = null,
    DateTimeOffset? ComboFromUtc = null, DateTimeOffset? ComboToUtc = null);

public sealed class RestaurantOrder : AuditableEntity
{
    private readonly List<RestaurantOrderLine> _lines = [];
    private readonly List<RestaurantEvent> _events = [];
    public Guid BranchId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Number { get; private set; } = "";
    public Guid ChannelId { get; private set; }
    public string ChannelName { get; private set; } = "";
    public string? ExternalReference { get; private set; }
    public RestaurantServiceType ServiceType { get; private set; }
    public Guid? TableTurnId { get; private set; }
    public string CustomerName { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string DeliveryZoneName { get; private set; } = "";
    public decimal DeliveryFee { get; private set; }
    public string Notes { get; private set; } = "";
    public string DraftJson { get; private set; } = "{}";
    public RestaurantOrderStatus Status { get; private set; } = RestaurantOrderStatus.Draft;
    public int Revision { get; private set; } = 1;
    public uint Version { get; private set; }
    public Guid? DriverId { get; private set; }
    public string? DriverName { get; private set; }
    public string? Route { get; private set; }
    public DateTimeOffset? AssignedAtUtc { get; private set; }
    public DateTimeOffset? DepartedAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public IReadOnlyCollection<RestaurantOrderLine> Lines => _lines;
    public IReadOnlyCollection<RestaurantEvent> Events => _events;
    public decimal Total => decimal.Round(_lines.Sum(x => x.ActiveTotal) + (Status == RestaurantOrderStatus.Cancelled ? 0 : DeliveryFee), 2);
    private RestaurantOrder() { }
    public RestaurantOrder(Guid branch, Guid warehouse, string number, RestaurantChoice channel, RestaurantServiceType service,
        string? external, string customer, string phone, string address, string zone, decimal fee, string notes, string draft)
    {
        if (!Enum.IsDefined(service) || branch == Guid.Empty || warehouse == Guid.Empty) throw new DomainRuleException("Revisa sucursal, bodega y servicio.");
        if (service == RestaurantServiceType.Delivery && (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(zone)))
            throw new DomainRuleException("Delivery requiere dirección, teléfono y zona.");
        BranchId = branch; WarehouseId = warehouse; Number = number; ChannelId = channel.Id; ChannelName = channel.Name;
        ServiceType = service; ExternalReference = string.IsNullOrWhiteSpace(external) ? null : external.Trim().ToUpperInvariant();
        CustomerName = customer.Trim(); Phone = phone.Trim(); Address = address.Trim(); DeliveryZoneName = zone;
        DeliveryFee = fee; Notes = notes.Trim(); DraftJson = draft;
    }
    public void CheckVersion(int revision) { if (Revision != revision) throw new DomainRuleException("El pedido cambió. Recarga antes de continuar."); }
    public void AddLines(IEnumerable<OrderLineSnapshot> lines)
    {
        if (Status is RestaurantOrderStatus.OutForDelivery or RestaurantOrderStatus.Delivered or RestaurantOrderStatus.Cancelled)
            throw new DomainRuleException("Este pedido ya no admite líneas.");
        foreach (var line in lines) _lines.Add(new RestaurantOrderLine(Id, Revision, line));
        if (_lines.Count == 0) throw new DomainRuleException("Agrega productos al pedido.");
    }
    public void ReplaceDraft(string draft, IEnumerable<OrderLineSnapshot> lines)
    { if (Status != RestaurantOrderStatus.Draft) throw new DomainRuleException("Un pedido enviado se corrige con líneas adicionales o cancelación."); _lines.Clear(); DraftJson = draft; AddLines(lines); }
    public void Submit(DateTimeOffset now) { if (Status != RestaurantOrderStatus.Draft) throw new DomainRuleException("El pedido ya fue enviado."); Status = RestaurantOrderStatus.Submitted; foreach (var line in _lines) line.Queue(now); }
    public void QueueNewLines(DateTimeOffset now) { foreach (var line in _lines.Where(x => !x.QueuedAtUtc.HasValue)) line.Queue(now); RefreshStatus(); }
    public void SetTable(Guid turnId) => TableTurnId = turnId;
    public void UpdateDraftDetails(Guid warehouse, RestaurantChoice channel, RestaurantServiceType service,
        string? external, string customer, string phone, string address, string zone, decimal fee, string notes, Guid? turnId)
    {
        if (Status != RestaurantOrderStatus.Draft) throw new DomainRuleException("Solo se editan los datos de un borrador.");
        if (!Enum.IsDefined(service) || warehouse == Guid.Empty) throw new DomainRuleException("Revisa bodega y servicio.");
        if (service == RestaurantServiceType.Delivery && (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(zone)))
            throw new DomainRuleException("Delivery requiere dirección, teléfono y zona.");
        WarehouseId = warehouse; ChannelId = channel.Id; ChannelName = channel.Name; ServiceType = service;
        ExternalReference = string.IsNullOrWhiteSpace(external) ? null : external.Trim().ToUpperInvariant();
        CustomerName = customer.Trim(); Phone = phone.Trim(); Address = address.Trim(); DeliveryZoneName = zone;
        DeliveryFee = fee; Notes = notes.Trim(); TableTurnId = turnId;
    }
    public (RestaurantOrderLine Preparing, RestaurantOrderLine Pending) SplitLine(RestaurantOrderLine original,
        OrderLineSnapshot preparing, OrderLineSnapshot pending, DateTimeOffset now)
    {
        if (!_lines.Contains(original) || Status is not (RestaurantOrderStatus.Submitted or RestaurantOrderStatus.InPreparation))
            throw new DomainRuleException("Solo se puede dividir una línea pendiente de cocina.");
        if (preparing.Quantity + pending.Quantity != original.ActiveQuantity || preparing.Gross - preparing.Discount + pending.Gross - pending.Discount != original.ActiveTotal)
            throw new DomainRuleException("La división debe conservar cantidad e importe.");
        original.MarkSplit();
        var first = new RestaurantOrderLine(Id, Revision + 1, preparing, original.Id);
        var second = new RestaurantOrderLine(Id, Revision + 1, pending, original.Id);
        first.Queue(original.QueuedAtUtc ?? now); second.Queue(original.QueuedAtUtc ?? now);
        _lines.Add(first); _lines.Add(second); return (first, second);
    }
    public void RefreshStatus()
    {
        if (Status is RestaurantOrderStatus.Draft or RestaurantOrderStatus.OutForDelivery or RestaurantOrderStatus.Delivered) return;
        var active = _lines.Where(x => x.ActiveQuantity > 0).ToList();
        Status = active.Count == 0 ? RestaurantOrderStatus.Cancelled
            : active.All(x => x.Status is KitchenLineStatus.Ready or KitchenLineStatus.HandedOff) ? RestaurantOrderStatus.Ready
            : active.Any(x => x.Status != KitchenLineStatus.Queued) ? RestaurantOrderStatus.InPreparation : RestaurantOrderStatus.Submitted;
    }
    public void CancelDraft() { if (Status != RestaurantOrderStatus.Draft) throw new DomainRuleException("Cancela las líneas del pedido enviado."); foreach (var line in _lines.Where(x => x.ActiveQuantity > 0)) line.Cancel(line.ActiveQuantity); Status = RestaurantOrderStatus.Cancelled; }
    public void AssignDriver(RestaurantChoice driver, string? route, DateTimeOffset now)
    {
        if (ServiceType != RestaurantServiceType.Delivery || Status != RestaurantOrderStatus.Ready || !driver.Active) throw new DomainRuleException("Asigna un motorista activo a un delivery listo.");
        DriverId = driver.Id; DriverName = driver.Name; Route = route?.Trim(); AssignedAtUtc = now;
    }
    public void Depart(DateTimeOffset now)
    { if (Status != RestaurantOrderStatus.Ready || !DriverId.HasValue || ServiceType != RestaurantServiceType.Delivery) throw new DomainRuleException("El delivery debe estar listo y asignado."); Status = RestaurantOrderStatus.OutForDelivery; DepartedAtUtc = now; }
    public void Deliver(DateTimeOffset now)
    {
        var expected = ServiceType == RestaurantServiceType.Delivery ? RestaurantOrderStatus.OutForDelivery : RestaurantOrderStatus.Ready;
        if (Status != expected) throw new DomainRuleException("El pedido todavía no está listo para entregar.");
        foreach (var line in _lines.Where(x => x.ActiveQuantity > 0)) line.HandOff(now);
        Status = RestaurantOrderStatus.Delivered; DeliveredAtUtc = now;
    }
    public void Record(string action, string actor, DateTimeOffset now, string detail, Guid? lineId = null)
    { Revision++; _events.Add(new RestaurantEvent(Id, lineId, Revision, action, actor, now, detail)); }
}

public sealed class RestaurantOrderLine : Entity
{
    public Guid OrderId { get; private set; }
    public int SentRevision { get; private set; }
    public string SnapshotJson { get; private set; } = "{}";
    public Guid ProductId { get; private set; }
    public Guid? ParentLineId { get; private set; }
    public Guid StationId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal CancelledQuantity { get; private set; }
    public decimal WasteQuantity { get; private set; }
    public decimal PreparedQuantity { get; private set; }
    public decimal ReturnedQuantity { get; private set; }
    public KitchenLineStatus Status { get; private set; } = KitchenLineStatus.Queued;
    public DateTimeOffset? QueuedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? ReadyAtUtc { get; private set; }
    public DateTimeOffset? HandedOffAtUtc { get; private set; }
    public decimal ActiveQuantity => Status == KitchenLineStatus.Split ? 0 : Quantity - CancelledQuantity;
    public decimal ActiveTotal => decimal.Round((Read().Gross - Read().Discount) * ActiveQuantity / Quantity, 2);
    private RestaurantOrderLine() { }
    public RestaurantOrderLine(Guid order, int revision, OrderLineSnapshot snapshot, Guid? parentLineId = null)
    {
        if (snapshot.Quantity <= 0 || snapshot.Gross < 0 || snapshot.Discount < 0 || snapshot.Discount > snapshot.Gross) throw new DomainRuleException("La línea contiene cantidades o importes inválidos.");
        OrderId = order; ParentLineId = parentLineId; SentRevision = revision; ProductId = snapshot.ProductId; StationId = snapshot.StationId; Quantity = snapshot.Quantity; SnapshotJson = JsonSerializer.Serialize(snapshot);
    }
    public OrderLineSnapshot Read() => JsonSerializer.Deserialize<OrderLineSnapshot>(SnapshotJson)!;
    public void Queue(DateTimeOffset now) => QueuedAtUtc ??= now;
    public void Start(DateTimeOffset now) { if (Status != KitchenLineStatus.Queued || !QueuedAtUtc.HasValue) throw new DomainRuleException("La línea debe estar en cola."); PreparedQuantity = ActiveQuantity; Status = KitchenLineStatus.Preparing; StartedAtUtc = now; }
    public void Ready(DateTimeOffset now) { if (Status != KitchenLineStatus.Preparing) throw new DomainRuleException("La línea debe estar en preparación."); Status = KitchenLineStatus.Ready; ReadyAtUtc = now; }
    public void HandOff(DateTimeOffset now) { if (Status != KitchenLineStatus.Ready) throw new DomainRuleException("Todas las líneas deben estar listas."); Status = KitchenLineStatus.HandedOff; HandedOffAtUtc = now; }
    public void MarkSplit()
    { if (Status != KitchenLineStatus.Queued || !QueuedAtUtc.HasValue) throw new DomainRuleException("Solo se divide una línea en cola."); Status = KitchenLineStatus.Split; }
    public void Cancel(decimal quantity, bool returnIntact = false)
    {
        if (quantity <= 0 || quantity > ActiveQuantity || quantity != decimal.Round(quantity, 2) || Status == KitchenLineStatus.HandedOff)
            throw new DomainRuleException("La cantidad a cancelar no es válida o ya fue entregada.");
        if (returnIntact && !StartedAtUtc.HasValue) throw new DomainRuleException("Una línea sin consumo solo libera la reserva.");
        CancelledQuantity += quantity;
        if (StartedAtUtc.HasValue) { if (returnIntact) ReturnedQuantity += quantity; else WasteQuantity += quantity; }
        if (ActiveQuantity == 0) Status = KitchenLineStatus.Cancelled;
    }
}

public sealed class OrderWaste(Guid orderId, Guid lineId, decimal quantity, decimal cost, string reason,
    string actor, DateTimeOffset atUtc, string consumptionJson) : Entity
{
    public Guid OrderId { get; private set; } = orderId;
    public Guid LineId { get; private set; } = lineId;
    public decimal Quantity { get; private set; } = quantity;
    public decimal Cost { get; private set; } = cost;
    public string Reason { get; private set; } = reason;
    public string Actor { get; private set; } = actor;
    public DateTimeOffset AtUtc { get; private set; } = atUtc;
    public string ConsumptionJson { get; private set; } = consumptionJson;
}

public sealed class RestaurantEvent(Guid orderId, Guid? lineId, int revision, string action, string actor, DateTimeOffset atUtc, string detail) : Entity
{
    public Guid OrderId { get; private set; } = orderId;
    public Guid? LineId { get; private set; } = lineId;
    public int Revision { get; private set; } = revision;
    public string Action { get; private set; } = action;
    public string Actor { get; private set; } = actor;
    public DateTimeOffset AtUtc { get; private set; } = atUtc;
    public string Detail { get; private set; } = detail;
}

public sealed class OrderReservation : Entity
{
    public Guid OrderId { get; private set; }
    public Guid LineId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? LotId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal RestoredQuantity { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Active;
    public Guid? MovementId { get; private set; }
    private OrderReservation() { }
    public OrderReservation(Guid order, Guid line, Guid product, Guid warehouse, Guid? lot, decimal quantity, decimal cost)
    { OrderId = order; LineId = line; ProductId = product; WarehouseId = warehouse; LotId = lot; Quantity = quantity; UnitCost = cost; }
    public void Consume(Guid movementId) { if (Status != ReservationStatus.Active) throw new DomainRuleException("La reserva ya fue procesada."); Status = ReservationStatus.Consumed; MovementId = movementId; }
    public void Restore(decimal quantity)
    { if (Status != ReservationStatus.Consumed || quantity <= 0 || RestoredQuantity + quantity > Quantity) throw new DomainRuleException("La devolución excede el consumo original."); RestoredQuantity += quantity; }
    public void Release(decimal fraction)
    { if (Status != ReservationStatus.Active || fraction <= 0 || fraction > 1) throw new DomainRuleException("Reserva no válida."); Quantity = decimal.Round(Quantity * (1 - fraction), 6); if (Quantity == 0) Status = ReservationStatus.Released; }
}

public sealed class TableTurn : AuditableEntity
{
    public Guid BranchId { get; private set; }
    public Guid TableId { get; private set; }
    public string TableName { get; private set; } = "";
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public uint Version { get; private set; }
    private TableTurn() { }
    public TableTurn(Guid branch, RestaurantTable table, DateTimeOffset now) { BranchId = branch; TableId = table.Id; TableName = $"{table.Area} / {table.Name}"; OpenedAtUtc = now; }
    public void Move(RestaurantTable table) { if (ClosedAtUtc.HasValue) throw new DomainRuleException("Turno cerrado."); TableId = table.Id; TableName = $"{table.Area} / {table.Name}"; }
    public void Close(DateTimeOffset now) { if (ClosedAtUtc.HasValue) throw new DomainRuleException("Turno ya cerrado."); ClosedAtUtc = now; }
}

public sealed class RestaurantCommand(Guid operationId, Guid branchId, string userName, string fingerprint, Guid resultId) : Entity
{
    public Guid OperationId { get; private set; } = operationId;
    public Guid BranchId { get; private set; } = branchId;
    public string UserName { get; private set; } = userName;
    public string Fingerprint { get; private set; } = fingerprint;
    public Guid ResultId { get; private set; } = resultId;
}
