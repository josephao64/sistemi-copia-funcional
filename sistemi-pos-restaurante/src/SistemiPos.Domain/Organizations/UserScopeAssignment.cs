using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Organizations;

public sealed class UserScopeAssignment : AuditableEntity
{
    public Guid UserId { get; private set; }
    public UserScopeLevel ScopeLevel { get; private set; }
    public Guid? BusinessGroupId { get; private set; }
    public BusinessGroup? BusinessGroup { get; set; }
    public Guid? CompanyId { get; private set; }
    public Company? Company { get; set; }
    public Guid? BranchId { get; private set; }
    public Branch? Branch { get; set; }
    public bool IsDefault { get; private set; }

    private UserScopeAssignment()
    {
    }

    public UserScopeAssignment(
        Guid userId,
        UserScopeLevel scopeLevel,
        Guid? businessGroupId = null,
        Guid? companyId = null,
        Guid? branchId = null,
        bool isDefault = false)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("El usuario es requerido para asignar el alcance.");
        }

        if (scopeLevel == UserScopeLevel.BusinessGroup && (!businessGroupId.HasValue || businessGroupId == Guid.Empty))
        {
            throw new DomainRuleException("El grupo empresarial es requerido para este alcance.");
        }

        if (scopeLevel == UserScopeLevel.Company && (!companyId.HasValue || companyId == Guid.Empty))
        {
            throw new DomainRuleException("La empresa es requerida para este alcance.");
        }

        if (scopeLevel == UserScopeLevel.Branch && (!branchId.HasValue || branchId == Guid.Empty))
        {
            throw new DomainRuleException("La sucursal es requerida para este alcance.");
        }

        UserId = userId;
        ScopeLevel = scopeLevel;
        BusinessGroupId = businessGroupId;
        CompanyId = companyId;
        BranchId = branchId;
        IsDefault = isDefault;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
