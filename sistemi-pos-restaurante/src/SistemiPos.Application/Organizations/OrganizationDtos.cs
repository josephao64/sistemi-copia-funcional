using SistemiPos.Domain.Organizations;

namespace SistemiPos.Application.Organizations;

public sealed record BusinessGroupDto(
    Guid Id,
    string Code,
    string Name,
    string? LegalName,
    string? TaxNumber,
    string? Description,
    bool IsActive,
    int CompanyCount);

public sealed record CreateBusinessGroupDto(
    string Code,
    string Name,
    string? LegalName,
    string? TaxNumber,
    string? Description);

public sealed record UpdateBusinessGroupDto(
    Guid Id,
    string Code,
    string Name,
    string? LegalName,
    string? TaxNumber,
    string? Description,
    bool IsActive);

public sealed record CompanyDto(
    Guid Id,
    Guid BusinessGroupId,
    string BusinessGroupName,
    string Code,
    string TradeName,
    string? LegalName,
    string? Nit,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxRegime,
    bool IsActive,
    int BranchCount);

public sealed record CreateCompanyDto(
    Guid BusinessGroupId,
    string Code,
    string TradeName,
    string? LegalName,
    string? Nit,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxRegime);

public sealed record UpdateCompanyDto(
    Guid Id,
    Guid BusinessGroupId,
    string Code,
    string TradeName,
    string? LegalName,
    string? Nit,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxRegime,
    bool IsActive);

public sealed record BranchDto(
    Guid Id,
    Guid CompanyId,
    string CompanyTradeName,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string TimeZone,
    string? EstablishmentCode,
    bool IsActive,
    int WarehouseCount);

public sealed record CreateBranchDto(
    Guid CompanyId,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string TimeZone,
    string? EstablishmentCode);

public sealed record UpdateBranchDto(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string TimeZone,
    string? EstablishmentCode,
    bool IsActive);

public sealed record WarehouseDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string CompanyName,
    string Code,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsKitchen,
    bool IsActive);

public sealed record CreateWarehouseDto(
    Guid BranchId,
    string Code,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsKitchen);

public sealed record UpdateWarehouseDto(
    Guid Id,
    Guid BranchId,
    string Code,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsKitchen,
    bool IsActive);

public sealed record UserScopeDto(
    Guid Id,
    Guid UserId,
    UserScopeLevel ScopeLevel,
    Guid? BusinessGroupId,
    string? BusinessGroupName,
    Guid? CompanyId,
    string? CompanyName,
    Guid? BranchId,
    string? BranchName,
    bool IsDefault);

public sealed record SaveUserScopesDto(
    Guid UserId,
    UserScopeLevel ScopeLevel,
    List<Guid> BranchIds,
    Guid? DefaultBranchId,
    Guid? BusinessGroupId,
    Guid? CompanyId);
