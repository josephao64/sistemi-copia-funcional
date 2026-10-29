using SistemiPos.Domain.Common;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Domain.Sales;

public sealed class DiscountApprovalRequest : AuditableEntity
{
    private readonly List<DiscountApprovalItem> _items = [];

    public Guid CashSessionId { get; private set; }
    public string RequestedByUserName { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public DiscountApprovalStatus Status { get; private set; } = DiscountApprovalStatus.Pending;
    public string? ResolvedByUserName { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public string? DecisionComment { get; private set; }
    public Guid? SaleId { get; private set; }
    public decimal GrossTotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal NetTotal { get; private set; }
    public uint RowVersion { get; private set; }
    public IReadOnlyCollection<DiscountApprovalItem> Items => _items;

    private DiscountApprovalRequest() { }

    public DiscountApprovalRequest(Guid cashSessionId, string requestedByUserName, DateTimeOffset requestedAtUtc,
        IEnumerable<DiscountApprovalItem> items)
    {
        if (cashSessionId == Guid.Empty) throw new DomainRuleException("La caja es requerida para solicitar el descuento.");
        if (string.IsNullOrWhiteSpace(requestedByUserName)) throw new DomainRuleException("El vendedor es requerido.");
        CashSessionId = cashSessionId;
        RequestedByUserName = requestedByUserName.Trim();
        RequestedAtUtc = requestedAtUtc;
        _items.AddRange(items);
        if (_items.Count == 0 || _items.All(x => x.DiscountAmount <= 0))
            throw new DomainRuleException("La solicitud debe incluir al menos un descuento.");
        GrossTotal = decimal.Round(_items.Sum(x => x.GrossAmount), 2);
        DiscountTotal = decimal.Round(_items.Sum(x => x.DiscountAmount), 2);
        NetTotal = GrossTotal - DiscountTotal;
    }

    public void Approve(string userName, DateTimeOffset resolvedAtUtc, string? comment)
    {
        EnsurePending();
        Status = DiscountApprovalStatus.Approved;
        Resolve(userName, resolvedAtUtc, comment);
    }

    public void Reject(string userName, DateTimeOffset resolvedAtUtc, string? comment)
    {
        EnsurePending();
        Status = DiscountApprovalStatus.Rejected;
        Resolve(userName, resolvedAtUtc, comment);
    }

    public void MarkUsed(Guid saleId)
    {
        if (Status != DiscountApprovalStatus.Approved) throw new DomainRuleException("La solicitud de descuento no está aprobada.");
        Status = DiscountApprovalStatus.Used;
        SaleId = saleId;
    }

    private void Resolve(string userName, DateTimeOffset atUtc, string? comment)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new DomainRuleException("El usuario que resuelve la solicitud es requerido.");
        ResolvedByUserName = userName.Trim();
        ResolvedAtUtc = atUtc;
        DecisionComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private void EnsurePending()
    {
        if (Status != DiscountApprovalStatus.Pending) throw new DomainRuleException("Esta solicitud ya fue resuelta.");
    }
}

public sealed class DiscountApprovalItem : Entity
{
    public Guid DiscountApprovalRequestId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public SaleDiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string? Comment { get; private set; }

    private DiscountApprovalItem() { }

    public DiscountApprovalItem(Guid productId, string productName, decimal quantity, decimal unitPrice,
        SaleDiscountType discountType, decimal discountValue, decimal grossAmount, decimal discountAmount, string? comment)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountType = discountType;
        DiscountValue = discountValue;
        GrossAmount = decimal.Round(grossAmount, 2);
        DiscountAmount = decimal.Round(discountAmount, 2);
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }
}
