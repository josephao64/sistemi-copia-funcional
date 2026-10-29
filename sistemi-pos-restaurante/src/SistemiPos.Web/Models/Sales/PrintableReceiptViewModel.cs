using SistemiPos.Application.Sales;
using SistemiPos.Application.Settings;

namespace SistemiPos.Web.Models.Sales;

public sealed class PrintableReceiptViewModel
{
    public required SaleDto Sale { get; init; }
    public required SystemSettingDto Settings { get; init; }
    public string CustomerNit { get; init; } = "CF";
}
