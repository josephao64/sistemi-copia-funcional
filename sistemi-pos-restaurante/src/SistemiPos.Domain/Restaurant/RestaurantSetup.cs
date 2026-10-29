using System.Text.Json;
using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Restaurant;

public enum RestaurantServiceType { DineIn = 1, Takeaway = 2, Delivery = 3 }
public sealed record RestaurantChoice(Guid Id, string Name, bool Active = true, bool Platform = false);
public sealed record RestaurantTable(Guid Id, string Name, string Area, bool Active = true);
public sealed record DeliveryZone(Guid Id, string Name, decimal Fee, bool Active = true);
public sealed record RestaurantMenuSection(Guid Id, string Name, int SortOrder, bool Active = true, string? Icon = null);
public sealed record ModifierComponent(Guid ProductId, decimal Quantity);
public sealed record ModifierOption(Guid Id, string Name, decimal Price, List<ModifierComponent> Components, bool Active = true, int SortOrder = 0);
public sealed record ModifierGroup(Guid Id, string Name, int Minimum, int Maximum, List<ModifierOption> Options, string? SelectionType = null, bool Active = true);
public sealed record RestaurantMenuItem(Guid ProductId, Guid StationId, bool Favorite, List<Guid> ExcludableProducts,
    List<ModifierGroup> Groups, int SortOrder = 0, Guid? SectionId = null,
    string? CustomName = null, string? Description = null, decimal? CustomPrice = null,
    bool Active = true, List<string>? Channels = null, bool AlwaysAvailable = true,
    string? AvailableFrom = null, string? AvailableTo = null,
    string? SuspensionReason = null, string? AvailabilityNotes = null);
public sealed record ComboGroup(Guid Id, string Name, int Quantity, List<Guid> ProductIds);
public sealed record RestaurantCombo(Guid Id, string Name, decimal Price, DateTimeOffset FromUtc,
    DateTimeOffset ToUtc, List<ComboGroup> Groups, bool Active = true);
public sealed record RestaurantPromotion(Guid Id, string Name, Guid ProductId, decimal Percent,
    int Priority, DateTimeOffset FromUtc, DateTimeOffset ToUtc, bool Active = true);
public sealed class RestaurantDefinition
{
    public List<RestaurantChoice> Channels { get; set; } = [];
    public List<RestaurantChoice> Stations { get; set; } = [];
    public List<RestaurantChoice> Drivers { get; set; } = [];
    public List<RestaurantTable> Tables { get; set; } = [];
    public List<DeliveryZone> Zones { get; set; } = [];
    public List<RestaurantMenuSection> Sections { get; set; } = [];
    public List<RestaurantMenuItem> Menu { get; set; } = [];
    public List<RestaurantCombo> Combos { get; set; } = [];
    public List<RestaurantPromotion> Promotions { get; set; } = [];

    public void Validate()
    {
        if (Channels is null || Stations is null || Drivers is null || Tables is null || Zones is null || Sections is null || Menu is null || Combos is null || Promotions is null
            || Channels.Any(x => x is null) || Stations.Any(x => x is null) || Drivers.Any(x => x is null) || Tables.Any(x => x is null) || Zones.Any(x => x is null)
            || Sections.Any(x => x is null)
            || Menu.Any(x => x is null || x.ExcludableProducts is null || x.Groups is null || x.Groups.Any(g => g is null || g.Options is null || g.Options.Any(o => o is null || o.Components is null || o.Components.Any(c => c is null))))
            || Combos.Any(x => x is null || x.Groups is null || x.Groups.Any(g => g is null || g.ProductIds is null)) || Promotions.Any(x => x is null))
            throw new DomainRuleException("La configuración contiene listas o selecciones incompletas.");
        if (Menu.Count > 2000 || Combos.Count > 500 || Tables.Count > 1000 || Sections.Count > 100) throw new DomainRuleException("La configuración excede los límites de operación de la sucursal.");
        static void Unique(IEnumerable<Guid> ids) { var values = ids.ToList(); if (values.Contains(Guid.Empty) || values.Distinct().Count() != values.Count) throw new DomainRuleException("Los identificadores de configuración deben ser únicos."); }
        foreach (var choices in new[] { Channels, Stations, Drivers })
        { Unique(choices.Select(x => x.Id)); if (choices.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 120)) throw new DomainRuleException("Indica nombres de hasta 120 caracteres."); }
        Unique(Tables.Select(x => x.Id)); Unique(Zones.Select(x => x.Id)); Unique(Sections.Select(x => x.Id)); Unique(Menu.Select(x => x.ProductId));
        Unique(Combos.Select(x => x.Id)); Unique(Promotions.Select(x => x.Id));
        if (Sections.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 80 || x.SortOrder < 0)
            || Sections.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new DomainRuleException("Las secciones del menú requieren nombres únicos de hasta 80 caracteres.");
        if (Tables.Any(x => string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Area))
            || Tables.GroupBy(x => (x.Area.Trim().ToUpperInvariant(), x.Name.Trim().ToUpperInvariant())).Any(x => x.Count() > 1))
            throw new DomainRuleException("Las mesas requieren área y nombre único en el área.");
        if (Zones.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Fee < 0 || decimal.Round(x.Fee, 2) != x.Fee)) throw new DomainRuleException("Revisa nombre y tarifa de entrega.");
        foreach (var item in Menu)
        {
            if (!Stations.Any(x => x.Id == item.StationId)) throw new DomainRuleException("Cada producto del menú requiere una estación.");
            if (item.CustomPrice.HasValue && (item.CustomPrice.Value < 0 || decimal.Round(item.CustomPrice.Value, 2) != item.CustomPrice.Value))
                throw new DomainRuleException("El precio de venta por sucursal debe ser positivo con dos decimales.");
            if (item.SectionId.HasValue && !Sections.Any(x => x.Id == item.SectionId.Value && x.Active))
                throw new DomainRuleException("Cada producto debe usar una sección activa de esta sucursal.");
            Unique(item.ExcludableProducts); Unique(item.Groups.Select(x => x.Id));
            Unique(item.Groups.SelectMany(x => x.Options).Select(x => x.Id));
            foreach (var group in item.Groups)
            {
                Unique(group.Options.Select(x => x.Id));
                if (string.IsNullOrWhiteSpace(group.Name) || group.Minimum < 0 || group.Maximum < group.Minimum || group.Maximum > group.Options.Count || group.Minimum > group.Options.Count(x => x.Active))
                    throw new DomainRuleException("Revisa mínimo y máximo de las opciones.");
                foreach (var option in group.Options)
                {
                    if (string.IsNullOrWhiteSpace(option.Name) || option.Price < 0 || decimal.Round(option.Price, 2) != option.Price)
                        throw new DomainRuleException("El extra requiere nombre y precio válido.");
                    Unique(option.Components.Select(x => x.ProductId));
                    if (option.Components.Any(x => x.Quantity <= 0 || x.Quantity != decimal.Round(x.Quantity, 6))) throw new DomainRuleException("El consumo adicional debe ser positivo, hasta seis decimales.");
                }
            }
        }
        foreach (var combo in Combos)
        {
            Unique(combo.Groups.Select(x => x.Id));
            foreach (var group in combo.Groups) Unique(group.ProductIds);
            if (string.IsNullOrWhiteSpace(combo.Name) || combo.Price < 0 || combo.Price != decimal.Round(combo.Price, 2) || combo.ToUtc <= combo.FromUtc
                || combo.Groups.Count == 0 || combo.Groups.Sum(x => (long)x.Quantity) > 100 || combo.Groups.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Quantity < 1 || x.ProductIds.Count == 0 || x.ProductIds.Any(id => Menu.All(m => m.ProductId != id))))
                throw new DomainRuleException("Revisa precio, vigencia y selecciones del combo.");
        }
        if (Promotions.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Percent <= 0 || x.Percent > 100 || x.ToUtc <= x.FromUtc || Menu.All(m => m.ProductId != x.ProductId)))
            throw new DomainRuleException("Revisa producto, porcentaje y vigencia de la promoción.");
    }
}

public sealed class RestaurantSetup : AuditableEntity
{
    public Guid BranchId { get; private set; }
    public int Revision { get; private set; }
    public string DefinitionJson { get; private set; } = "{}";
    public uint Version { get; private set; }
    private RestaurantSetup() { }
    public RestaurantSetup(Guid branchId) { BranchId = branchId; }
    public RestaurantDefinition Read() => JsonSerializer.Deserialize<RestaurantDefinition>(DefinitionJson)!;
    public void Update(RestaurantDefinition definition, int expectedRevision)
    {
        if (Revision != expectedRevision) throw new DomainRuleException("La configuración cambió. Recarga antes de guardar.");
        definition.Validate(); DefinitionJson = JsonSerializer.Serialize(definition); Revision++;
    }
}
