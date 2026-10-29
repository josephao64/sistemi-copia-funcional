namespace SistemiPos.Application.Settings;

public sealed record RoleDiscountSettingDto(
    Guid Id,
    Guid RoleId,
    decimal MaxDiscountPercent);
