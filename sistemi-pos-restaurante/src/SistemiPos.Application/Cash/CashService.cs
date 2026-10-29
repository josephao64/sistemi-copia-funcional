using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Cash;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Application.Cash;

public sealed class CashService(
    ICashRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUserAccessor,
    IDateTimeProvider dateTimeProvider,
    IOperationAuthorizationService? authorizationService = null,
    IOperationContextAccessor? operationContext = null,
    CashAuthorizationPolicy? cashAuthorizationPolicy = null) : ICashService
{
    public async Task<IReadOnlyList<CashSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<CashSessionDto>();
        foreach (var session in (await repository.GetSessionsAsync(cancellationToken)).OrderByDescending(x => x.OpenedAtUtc))
            result.Add(await ProtectBlindCountAsync(session, cancellationToken));
        return result;
    }

    public async Task<CashSessionDto?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await repository.GetSessionAsync(id, cancellationToken);
        return session is null ? null : await ProtectBlindCountAsync(session, cancellationToken);
    }

    public async Task<CashSessionDto?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await repository.GetActiveSessionByUserAsync(currentUserAccessor.UserId, currentUserAccessor.UserName, cancellationToken);
        return session is null ? null : await ProtectBlindCountAsync(session, cancellationToken);
    }

    public async Task<CashCutDto> GetCutAsync(Guid id, bool final, CancellationToken ct = default)
    {
        if (authorizationService is not null && !await authorizationService.HasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.CashClosingReport, ct))
            throw new DomainRuleException("No tienes permiso para consultar cortes de caja.");
        var session = await repository.GetSessionAsync(id, ct) ?? throw new NotFoundException("Caja no encontrada.");
        await ProtectBlindCountAsync(session, ct);
        if (final && session.Status != CashSessionStatus.Closed)
            throw new DomainRuleException("El corte Z requiere una caja cerrada.");
        if (!final && session.Status != CashSessionStatus.Active)
            throw new DomainRuleException("El corte X requiere una caja activa.");
        if (session.Counts.Count == 0 && session.Status == CashSessionStatus.Active && authorizationService is not null
            && await authorizationService.HasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.CashCountHideExpected, ct))
            throw new DomainRuleException("Declara el efectivo antes de consultar el esperado.");
        decimal Sum(SistemiPos.Domain.Cash.CashMovementType type) => session.Movements.Where(x => x.Type == type).Sum(x => x.Amount);
        return new(session.Id, session.RegisterCode, session.CashierUserName, final, final ? session.ClosedAtUtc!.Value : dateTimeProvider.UtcNow,
            final ? session.ExpectedCash!.Value : session.CalculateExpectedCash(), session.CountedCash, session.Difference,
            final ? session.TotalCashSales ?? 0 : Sum(CashMovementType.SaleCash), final ? session.TotalTransferSales ?? 0 : Sum(CashMovementType.SaleTransfer),
            final ? session.TotalPosSales ?? 0 : Sum(CashMovementType.SalePos), final ? session.TotalOtherSales ?? 0 : Sum(CashMovementType.SaleOther), session.CurrencySnapshotJson);
    }

    public async Task<Guid> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken = default)
    {
        var registerCode = string.IsNullOrWhiteSpace(request.RegisterCode) ? $"USR-{currentUserAccessor.UserName}" : request.RegisterCode.Trim().ToUpperInvariant();
        if (await repository.GetActiveSessionByRegisterAsync(registerCode, cancellationToken) is not null)
            throw new DomainRuleException("La caja física ya tiene una sesión activa.");
        if (await repository.GetActiveSessionByUserAsync(currentUserAccessor.UserId, currentUserAccessor.UserName, cancellationToken) is not null)
            throw new DomainRuleException("El usuario ya tiene una caja abierta.");

        var posPaymentsEnabled = await repository.IsPosPaymentEnabledAsync(cancellationToken);
        var usePosDevices = posPaymentsEnabled && request.UsePosDevices;
        var requestedPosIds = usePosDevices
            ? (request.PosDeviceIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList()
            : [];
        if (usePosDevices && requestedPosIds.Count == 0)
            throw new DomainRuleException("Seleccione al menos un POS bancario para abrir la caja.");
        var posDevices = await repository.GetActivePosDevicesAsync(requestedPosIds, cancellationToken);
        if (posDevices.Count != requestedPosIds.Count)
            throw new DomainRuleException("Uno o más POS seleccionados no existen o están inactivos.");
        var assignedPosIds = await repository.GetPosDeviceIdsAssignedToActiveSessionsAsync(requestedPosIds, cancellationToken);
        if (assignedPosIds.Count > 0)
            throw new DomainRuleException("Uno o más POS seleccionados ya están siendo utilizados por otra caja abierta.");

        var session = new CashSession(
            registerCode, currentUserAccessor.UserName, dateTimeProvider.UtcNow,
            request.OpeningAmount, currentUserAccessor.UserId, request.Observation,
            branchId: operationContext?.CurrentBranchId);
        var currencySetting = await repository.GetSystemSettingAsync(cancellationToken);
        if (request.TerminalId.HasValue) session.BindTerminal(request.TerminalId.Value, request.Timezone ?? "America/Guatemala");
        session.CaptureCurrency(currencySetting ?? SistemiPos.Domain.Configuration.SystemSetting.CreateDefault());
        foreach (var posDevice in posDevices) session.AssignPosDevice(posDevice.Id, posDevice.Name);
        await repository.AddSessionAsync(session, cancellationToken);
        await AuditAsync("Apertura de caja", session.Id, session.OpeningAmount, null, request.Observation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    public async Task<CashCountDto> RecordCountAsync(Guid id, RecordCashCountRequest request, CancellationToken cancellationToken = default)
    {
        var session = await OwnedActiveSessionAsync(id, cancellationToken);
        var denominations = (await repository.GetActiveDenominationsAsync(cancellationToken)).ToDictionary(x => x.Id);
        if (denominations.Count == 0) throw new DomainRuleException("Configure denominaciones activas antes de realizar el arqueo.");
        var requested = request.Lines.GroupBy(x => x.DenominationId).Select(x => new { Id = x.Key, Quantity = x.Sum(y => y.Quantity) }).ToList();
        if (requested.Any(x => x.Quantity < 0 || !denominations.ContainsKey(x.Id))) throw new DomainRuleException("El conteo contiene denominaciones no válidas.");
        var lines = requested.Select(x => new CashCountLine(x.Id, denominations[x.Id].Value, x.Quantity)).ToList();
        var count = new CashCount(session.Id, 1, dateTimeProvider.UtcNow,
            currentUserAccessor.UserName, session.CalculateExpectedCash(), lines);
        session.AddCount(count);
        // The session is already tracked. Register the new dependent explicitly so EF
        // always inserts the count and its lines instead of inferring their state from
        // the navigation graph of an existing session.
        await repository.AddCountAsync(count, cancellationToken);
        await AuditAsync("Arqueo de cierre", session.Id, count.CountedCash, null,
            $"Diferencia: {count.Difference:0.00}", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(count);
    }

    public async Task<IReadOnlyList<UsedPosTotalDto>> GetUsedPosTotalsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repository.GetUsedPosTotalsAsync(id, cancellationToken);

    public async Task RecordPosClosingAsync(Guid id, RecordPosClosingRequest request, CancellationToken cancellationToken = default)
    {
        var session = await OwnedActiveSessionAsync(id, cancellationToken);
        var assigned = session.AssignedPosDevices.SingleOrDefault(x => x.PosDeviceId == request.PosDeviceId)
            ?? throw new DomainRuleException("El POS no fue asignado a esta caja.");
        var used = (await repository.GetUsedPosTotalsAsync(id, cancellationToken)).SingleOrDefault(x => x.PosDeviceId == request.PosDeviceId);
        if (session.PosClosings.Any(x => x.PosDeviceId == request.PosDeviceId)) throw new DomainRuleException("El cierre de este POS ya fue registrado.");
        var closing = new CashPosClosing(id, assigned.PosDeviceId, assigned.PosDeviceName, used?.SystemTotal ?? 0m, request.PhysicalTotal, request.BatchReference, request.ReceiptPath);
        session.AddPosClosing(closing);
        await repository.AddPosClosingAsync(closing, cancellationToken);
        await AuditAsync("Cierre POS", id, request.PhysicalTotal, null, $"{assigned.PosDeviceName}; diferencia: {closing.Difference:0.00}", cancellationToken,
            "PosDevice", assigned.PosDeviceId, request.BatchReference);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseSessionAsync(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken = default)
    {
        var session = await OwnedActiveSessionAsync(id, cancellationToken);
        if (await repository.HasPendingPaymentsAsync(id, cancellationToken))
            throw new DomainRuleException("Resuelve los pagos pendientes antes de cerrar la caja.");
        if (session.AssignedPosDevices.Any(x => session.PosClosings.All(y => y.PosDeviceId != x.PosDeviceId)))
            throw new DomainRuleException("Registre el cierre físico de todos los POS asignados antes de cerrar la caja.");
        var counted = session.Counts.OrderBy(x => x.Sequence).LastOrDefault()?.CountedCash ?? request.CountedCash;
        if (counted < 0 || decimal.Round(counted, 2) != counted)
            throw new DomainRuleException("La declaración de efectivo debe ser un importe monetario válido.");
        if (session.Counts.Count > 0 && decimal.Round(request.CountedCash, 2) != counted)
            throw new DomainRuleException("El efectivo contado debe coincidir con el último arqueo.");

        string? differenceAuthorizer = null;
        if (counted != session.CalculateExpectedCash() && string.IsNullOrWhiteSpace(request.Observation))
            throw new DomainRuleException("El cierre con diferencia requiere un motivo.");
        if (counted != session.CalculateExpectedCash() && authorizationService is not null && cashAuthorizationPolicy?.SkipAdditionalCredentials != true
            && (request.RequireAuthorization || !await authorizationService.HasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.CashCloseDifferenceWithoutAuthorization, cancellationToken)))
            differenceAuthorizer = await authorizationService.ValidateCredentialsAsync(request.AuthorizerUserName ?? string.Empty,
                request.AuthorizerPassword ?? string.Empty, PermissionCodes.CashDifferenceAuthorize, cancellationToken);
        session.Close(dateTimeProvider.UtcNow, counted, differenceAuthorizer, request.Observation, currentUserAccessor.UserName);
        await AuditAsync("Cierre de caja", session.Id, counted, currentUserAccessor.UserName,
            $"Esperado: {session.ExpectedCash:0.00}; diferencia: {session.Difference:0.00}. {request.Observation}{(cashAuthorizationPolicy?.SkipAdditionalCredentials == true && session.Difference != 0 ? " [sin autorización adicional: desarrollo]" : "")}".Trim(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordMovementAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Type is not CashMovementType.OtherIncome and not CashMovementType.Expense)
            throw new DomainRuleException("Solo se admiten entradas o salidas manuales.");
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount)
            throw new DomainRuleException("El importe debe ser positivo y tener como máximo dos decimales.");
        var session = await OwnedActiveSessionAsync(id, cancellationToken);
        string? authorizedBy = null;
        if (authorizationService is not null && cashAuthorizationPolicy?.SkipAdditionalCredentials != true &&
            !await authorizationService.HasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.CashMovementsWithoutAuthorization, cancellationToken))
        {
            authorizedBy = await authorizationService.ValidateCredentialsAsync(
                request.AuthorizerUserName ?? string.Empty, request.AuthorizerPassword ?? string.Empty,
                PermissionCodes.CashMovementsAuthorize, cancellationToken);
        }

        var movement = new CashMovement(request.Type, request.Amount, request.Reason, currentUserAccessor.UserName,
            dateTimeProvider.UtcNow, authorizedBy, request.SourceType, request.SourceId, request.SourceDocument)
        { CashSessionId = session.Id };
        session.AddMovement(movement);
        await repository.AddMovementAsync(movement, cancellationToken);
        await AuditAsync(request.Type.ToString(), session.Id, request.Amount, authorizedBy, request.Reason + (cashAuthorizationPolicy?.SkipAdditionalCredentials == true ? " [sin autorización adicional: desarrollo]" : ""), cancellationToken,
            request.SourceType, request.SourceId, request.SourceDocument);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task RecordAdjustmentAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
        throw new DomainRuleException("Una caja cerrada es inmutable. Registre la corrección como una nueva operación auditada en una caja activa.");

    public async Task ReconcileSessionAsync(Guid id, string? observation, CancellationToken cancellationToken = default)
    {
        var session = await repository.GetSessionAsync(id, cancellationToken)
            ?? throw new NotFoundException("Caja no encontrada.");
        session.MarkReconciled(dateTimeProvider.UtcNow, currentUserAccessor.UserName, observation);
        await AuditAsync("Diferencia de caja cuadrada", session.Id, session.Difference,
            currentUserAccessor.UserName, observation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<CashSession> OwnedActiveSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionAsync(id, cancellationToken) ?? throw new NotFoundException("Caja no encontrada.");
        if (operationContext?.CurrentBranchId is Guid branch && session.BranchId != branch)
            throw new DomainRuleException("La caja no pertenece a la sucursal operativa.");
        if (session.Status != CashSessionStatus.Active) throw new DomainRuleException("La caja está cerrada y es inmutable.");
        var ownsById = currentUserAccessor.UserId is not null && session.CashierUserId == currentUserAccessor.UserId;
        if (!ownsById && !string.Equals(session.CashierUserName, currentUserAccessor.UserName, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("Solo el usuario propietario puede operar esta caja.");
        return session;
    }

    private Task AuditAsync(string action, Guid sessionId, decimal? amount, string? authorizedBy, string? comment,
        CancellationToken cancellationToken, string? documentType = null, Guid? documentId = null, string? documentReference = null) =>
        repository.AddFinancialAuditAsync(new FinancialAuditEntry
        {
            OccurredAtUtc = dateTimeProvider.UtcNow, UserName = currentUserAccessor.UserName, Action = action,
            CashSessionId = sessionId, Amount = amount, AuthorizedByUserName = authorizedBy, Comment = comment,
            DocumentType = documentType, DocumentId = documentId, DocumentReference = documentReference
        }, cancellationToken);

    private static CashCountDto Map(CashCount x) => new(x.Id, x.Sequence, x.CountedAtUtc, x.CountedByUserName,
        x.ExpectedCash, x.CountedCash, x.Difference, x.Lines.Select(y => new CashCountLineDto(y.DenominationId, y.DenominationValue, y.Quantity, y.Subtotal)).ToList());

    private async Task<CashSessionDto> ProtectBlindCountAsync(CashSession session, CancellationToken ct)
    {
        if (operationContext?.CurrentBranchId is Guid branch && session.BranchId != branch)
            throw new DomainRuleException("La caja no pertenece a la sucursal operativa.");
        var dto = Map(session);
        if (session.Status == CashSessionStatus.Active && session.Counts.Count == 0 && authorizationService is not null
            && await authorizationService.HasPermissionAsync(currentUserAccessor.UserName, PermissionCodes.CashCountHideExpected, ct))
            return dto with { OpeningAmount = 0, Movements = [], ExpectedCash = null, Difference = null,
                TotalSold = null, TotalCashSales = null, TotalTransferSales = null, TotalPosSales = null, OtherIncomeTotal = null, ExpenseTotal = null };
        return dto;
    }

    private static CashSessionDto Map(CashSession session) => new(
        session.Id, session.RegisterCode, session.CashierUserName, session.OpenedAtUtc, session.OpeningAmount, session.Status,
        session.ClosedAtUtc, session.CountedCash, session.TotalSold, session.TotalCashSales, session.TotalTransferSales,
        session.TotalPosSales, session.OtherIncomeTotal, session.ExpenseTotal, session.ExpectedCash, session.Difference,
        session.Movements.OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new CashMovementDto(x.Id, x.Type, x.Amount, x.Reason, x.UserName, x.OccurredAtUtc,
                x.AuthorizedByUserName, x.SourceType, x.SourceId, x.SourceDocument)).ToList(),
        session.Counts.OrderBy(x => x.Sequence).Select(Map).ToList(), session.OpeningObservation,
        session.ClosingObservation, session.DifferenceAuthorizedByUserName,
        session.PosClosings.Select(x => new CashPosClosingDto(x.Id, x.PosDeviceId, x.PosDeviceName, x.SystemTotal, x.PhysicalTotal, x.Difference, x.BatchReference, x.ReceiptPath)).ToList(),
        session.ReconciledAtUtc, session.ReconciledByUserName, session.ReconciliationObservation,
        session.AssignedPosDevices.OrderBy(x => x.PosDeviceName).Select(x => new CashSessionPosDeviceDto(x.PosDeviceId, x.PosDeviceName)).ToList(),
        session.BranchId, session.TotalOtherSales, session.TotalOtherRefunds, session.CurrencySnapshotJson);
}
