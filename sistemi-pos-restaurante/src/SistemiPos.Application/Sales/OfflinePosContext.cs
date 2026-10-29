namespace SistemiPos.Application.Sales;

public sealed record OfflinePosContext(Guid UserId, string UserName, Guid BranchId, Guid WarehouseId,
    Guid TerminalId, Guid CashSessionId, DateTimeOffset CapturedAtUtc, string CurrencyCode)
{
    public string Key => $"{UserId:N}:{BranchId:N}:{WarehouseId:N}:{TerminalId:N}:{CashSessionId:N}";
}
public sealed record OfflinePosTicket(OfflinePosContext Context, string Ticket);
public interface IOfflinePosProtector
{
    string Protect(OfflinePosContext context);
    OfflinePosContext Unprotect(string ticket);
}
