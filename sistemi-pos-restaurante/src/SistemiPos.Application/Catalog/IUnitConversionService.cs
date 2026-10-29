namespace SistemiPos.Application.Catalog;

public interface IUnitConversionService
{
    Task<decimal> ConvertAsync(decimal quantity, Guid fromUnitId, Guid toUnitId, Guid? productId = null, CancellationToken cancellationToken = default);
    Task<decimal> ConvertByCodeAsync(decimal quantity, string fromCode, string toCode, Guid? productId = null, CancellationToken cancellationToken = default);
    Task<bool> AreCompatibleAsync(Guid fromUnitId, Guid toUnitId, Guid? productId = null, CancellationToken cancellationToken = default);
}
