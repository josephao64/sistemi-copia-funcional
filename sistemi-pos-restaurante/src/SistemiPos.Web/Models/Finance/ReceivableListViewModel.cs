using Microsoft.AspNetCore.Mvc.Rendering;
using SistemiPos.Application.Finance;
using SistemiPos.Domain.Finance;

namespace SistemiPos.Web.Models.Finance;

public sealed class ReceivableListViewModel
{
    public ReceivableSection Section { get; init; }
    public string Title { get; init; } = "Cuentas por cobrar";
    public string Action { get; init; } = "Index";
    public IReadOnlyList<ReceivableAccountDto> Accounts { get; init; } = [];
    public ReceivableSummaryDto Summary { get; init; } = new(0, 0, 0, 0, 0, 0, 0, 0);
    public Guid? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public AccountStatus? Status { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? Search { get; init; }
    public IReadOnlyList<SelectListItem> Customers { get; init; } = [];
    public string CurrencySymbol { get; init; } = "Q";
    public int CurrencyDecimals { get; init; } = 2;
}
