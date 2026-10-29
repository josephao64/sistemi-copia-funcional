namespace SistemiPos.Application.Catalog;

public sealed record SaveCategoryRequest(string Name, string? Description, bool IsActive);
