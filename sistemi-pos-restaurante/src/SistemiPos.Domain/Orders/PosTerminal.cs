using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.Domain.Orders;

public sealed class PosTerminal : AuditableEntity
{
    public Guid BranchId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string RegisterCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    private PosTerminal() { }

    public PosTerminal(Guid branchId, Warehouse warehouse, string code, string registerCode)
    {
        BranchId = branchId;
        Update(warehouse, code, registerCode);
    }

    public void Update(Warehouse warehouse, string code, string registerCode)
    {
        if (BranchId == Guid.Empty || warehouse.BranchId != BranchId || !warehouse.IsActive)
            throw new DomainRuleException("La terminal requiere una bodega activa de su misma sucursal.");
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50
            || string.IsNullOrWhiteSpace(registerCode) || registerCode.Trim().Length > 50)
            throw new DomainRuleException("Los códigos de terminal y caja son obligatorios y admiten hasta 50 caracteres.");
        WarehouseId = warehouse.Id;
        Code = code.Trim().ToUpperInvariant();
        RegisterCode = registerCode.Trim().ToUpperInvariant();
    }
}
