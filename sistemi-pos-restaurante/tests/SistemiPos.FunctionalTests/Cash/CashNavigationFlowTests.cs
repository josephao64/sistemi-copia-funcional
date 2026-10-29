using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Cash;
using SistemiPos.Domain.Cash;
using SistemiPos.Web.Controllers;

namespace SistemiPos.FunctionalTests.Cash;

public sealed class CashNavigationFlowTests
{
    [Fact]
    public async Task Cash_Index_Redirects_To_Open_When_Cashier_Has_No_Active_Session()
    {
        var controller = new CashController(new FakeCashService(null), null!, null!);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Index(CancellationToken.None));

        Assert.Equal(nameof(CashController.Open), result.ActionName);
    }

    [Fact]
    public async Task Cash_Index_Redirects_To_Current_Session_Details_When_Cashier_Has_Active_Session()
    {
        var sessionId = Guid.NewGuid();
        var controller = new CashController(new FakeCashService(CreateSession(sessionId)), null!, null!);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Index(CancellationToken.None));

        Assert.Equal(nameof(CashController.Details), result.ActionName);
        Assert.Equal(sessionId, result.RouteValues!["id"]);
    }

    private static CashSessionDto CreateSession(Guid id) => new(
        id,
        "USR-cajero",
        "cajero",
        DateTimeOffset.UtcNow,
        100m,
        CashSessionStatus.Active,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        []);

    private sealed class FakeCashService(CashSessionDto? currentSession) : ICashService
    {
        public Task<IReadOnlyList<CashSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CashSessionDto?> GetSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guid> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task CloseSessionAsync(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordMovementAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RecordAdjustmentAsync(Guid id, RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CashSessionDto?> GetCurrentSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(currentSession);
    }
}
