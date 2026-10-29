using SistemiPos.Domain.Configuration;

namespace SistemiPos.Application.Cash;

public interface IPosCashService
{
    Task<PosCashState> StateAsync(Guid? terminalId, CancellationToken ct = default);
    Task<PosCashMutationResult> OpenAsync(PosCashOpen request, string key, CancellationToken ct = default);
    Task<PosCashMutationResult> MovementAsync(PosCashMovement request, string key, CancellationToken ct = default);
    Task<PosCashMutationResult> CloseAsync(PosCashClose request, string key, CancellationToken ct = default);
    Task<PosCashMutationResult?> ResultAsync(Guid terminalId, string kind, string key, CancellationToken ct = default);
    Task<PosCashState> ReceiptAsync(Guid sessionId, CancellationToken ct = default);
}

public sealed record PosCashPermissions(bool CanOpen, bool CanOperate, bool CanClose, bool CanIncome,
    bool CanExpense, bool CanSeeTotals, bool NeedsMovementAuthorization, bool NeedsOpeningPassword, bool NeedsClosingPassword, bool CanReceipt);
public sealed record PosCashSession(Guid Id, string Status, string Cashier, DateTimeOffset OpenedAtUtc,
    decimal? OpeningAmount, long Revision, PosCashSummary? Summary, DateTimeOffset? ClosedAtUtc, string? ClosedBy);
public sealed record PosCashSummary(decimal CashSales, decimal CardSales, decimal TransferSales, decimal OtherSales,
    decimal Income, decimal Expenses, decimal CashRefunds, decimal OtherCashOut, decimal ExpectedCash,
    decimal? CountedCash, decimal? Difference, decimal Collections, IReadOnlyList<PosCashMovementLine> Movements);
public sealed record PosCashMovementLine(Guid Id, string Type, decimal Amount, string Reason,
    string User, DateTimeOffset AtUtc, string? Reference, bool CanReverse);
public sealed record PosCashState(DateTimeOffset ServerNow, Guid BranchId, string Branch, Guid? TerminalId,
    string? Terminal, string Timezone, CurrencySnapshot Currency, string Status, PosCashSession? Session,
    PosCashPermissions Permissions, decimal? MaximumOpeningAmount, decimal? LongShiftHours, string CurrentUser, CurrencySnapshot OpeningCurrency);
public sealed record PosCashOpen(Guid TerminalId, [property: System.Text.Json.Serialization.JsonRequired] decimal OpeningAmount, string? Note = null,
    IReadOnlyList<Guid>? PosDeviceIds = null, string? Password = null);
public sealed record PosCashMovement(Guid TerminalId, Guid SessionId, bool IsIncome, decimal Amount, string Reason,
    string? Reference = null, string? AuthorizerUserName = null, string? AuthorizerPassword = null, Guid? ReversalOfId = null);
public sealed record PosCashClose(Guid TerminalId, Guid SessionId, [property: System.Text.Json.Serialization.JsonRequired] long ExpectedRevision, [property: System.Text.Json.Serialization.JsonRequired] decimal CountedCash,
    string? Note = null, string? AuthorizerUserName = null, string? AuthorizerPassword = null, string? Password = null);
public sealed record PosCashMutationResult(Guid SessionId, bool Replayed, PosCashState State);
public sealed class CashOperationException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record CashAuthorizationPolicy(bool SkipAdditionalCredentials = false);

public sealed class PosCashPolicy
{
    public decimal? MaximumOpeningAmount { get; set; }
    public decimal? DifferenceApprovalThreshold { get; set; }
    public decimal? LongShiftHours { get; set; }
}
public sealed class PosCashOptions
{
    public PosCashPolicy Default { get; set; } = new();
    public Dictionary<string, PosCashPolicy> Companies { get; set; } = [];
    public Dictionary<string, PosCashPolicy> Branches { get; set; } = [];
    public PosCashPolicy For(Guid company, Guid branch) => Branches.GetValueOrDefault(branch.ToString())
        ?? Companies.GetValueOrDefault(company.ToString()) ?? Default;
    public bool IsValid() => Companies.Keys.Concat(Branches.Keys).All(x => Guid.TryParse(x, out _))
        && Companies.Values.Concat(Branches.Values).Append(Default).All(p => p.MaximumOpeningAmount is null or >= 0
            && p.DifferenceApprovalThreshold is null or >= 0 && p.LongShiftHours is null or > 0);
}
