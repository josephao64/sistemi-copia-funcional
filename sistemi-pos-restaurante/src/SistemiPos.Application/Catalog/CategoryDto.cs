namespace SistemiPos.Application.Catalog;

public sealed record CategoryDto(Guid Id, string Name, string? Description, bool IsActive);
