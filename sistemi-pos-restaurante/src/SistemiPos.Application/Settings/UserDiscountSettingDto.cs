namespace SistemiPos.Application.Settings;

public sealed record UserDiscountSettingDto(
    Guid Id,
    Guid UserId,
    decimal MaxDiscountPercent);
