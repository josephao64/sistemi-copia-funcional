namespace SistemiPos.Application.Settings;

public sealed record SaveUserDiscountSettingRequest(
    Guid UserId,
    decimal MaxDiscountPercent);
