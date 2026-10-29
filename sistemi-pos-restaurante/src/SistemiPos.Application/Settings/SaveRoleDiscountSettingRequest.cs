namespace SistemiPos.Application.Settings;

public sealed record SaveRoleDiscountSettingRequest(
    Guid RoleId,
    decimal MaxDiscountPercent);
