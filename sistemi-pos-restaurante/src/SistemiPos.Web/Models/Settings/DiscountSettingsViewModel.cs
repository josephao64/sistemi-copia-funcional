using Microsoft.AspNetCore.Mvc.Rendering;

namespace SistemiPos.Web.Models.Settings;

public sealed class DiscountSettingsViewModel
{
    public Guid SelectedRoleId { get; set; }
    public decimal RoleMaxDiscountPercent { get; set; }
    public Guid SelectedUserId { get; set; }
    public decimal UserMaxDiscountPercent { get; set; }
    public IReadOnlyList<SelectListItem> Roles { get; set; } = [];
    public IReadOnlyList<SelectListItem> Users { get; set; } = [];
    public IReadOnlyList<(string Name, decimal MaxDiscountPercent)> RoleSettings { get; set; } = [];
    public IReadOnlyList<(string Name, decimal MaxDiscountPercent)> UserSettings { get; set; } = [];
}
