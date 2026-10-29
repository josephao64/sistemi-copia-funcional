using SistemiPos.Domain.Restaurant;
namespace SistemiPos.Web.Models.Restaurant;
public static class RestaurantLabels
{
    public static string Action(string action) => action switch
    {
        "submit" => "Envío a cocina", "start" => "Inicio de preparación", "ready" => "Preparación lista",
        "deliver" => "Entrega confirmada", "cancel" => "Cancelación", "assign" => "Asignación de motorista",
        "depart" => "Salida a reparto", "incident" => "Incidencia de entrega",
        "move-table" => "Cambio de mesa", "close-table" => "Liberación de mesa", _ => action
    };
    public static string Order(RestaurantOrderStatus state) => state switch { RestaurantOrderStatus.Draft => "Borrador", RestaurantOrderStatus.Submitted => "Enviado", RestaurantOrderStatus.InPreparation => "En preparación", RestaurantOrderStatus.Ready => "Listo", RestaurantOrderStatus.OutForDelivery => "En ruta", RestaurantOrderStatus.Delivered => "Entregado", RestaurantOrderStatus.Cancelled => "Cancelado", _ => state.ToString() };
    public static string Line(KitchenLineStatus state) => state switch { KitchenLineStatus.Queued => "En cola", KitchenLineStatus.Preparing => "Preparando", KitchenLineStatus.Ready => "Listo", KitchenLineStatus.HandedOff => "Entregado", KitchenLineStatus.Cancelled => "Cancelado", KitchenLineStatus.Split => "Dividida en preparación parcial", _ => state.ToString() };
    public static string Service(RestaurantServiceType type) => type switch { RestaurantServiceType.DineIn => "Salón", RestaurantServiceType.Takeaway => "Para llevar", RestaurantServiceType.Delivery => "Delivery", _ => type.ToString() };
}
