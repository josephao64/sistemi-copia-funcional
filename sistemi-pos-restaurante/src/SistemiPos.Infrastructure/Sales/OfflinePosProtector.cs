using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SistemiPos.Application.Sales;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Infrastructure.Sales;

public sealed class OfflinePosProtector(IDataProtectionProvider provider) : IOfflinePosProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("SistemiPos.OfflinePos.Context.v1");
    public string Protect(OfflinePosContext context) => _protector.Protect(JsonSerializer.Serialize(context));
    public OfflinePosContext Unprotect(string ticket)
    {
        try { return JsonSerializer.Deserialize<OfflinePosContext>(_protector.Unprotect(ticket)) ?? throw new InvalidOperationException(); }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or InvalidOperationException or ArgumentException)
        { throw new DomainRuleException("El contexto offline no es válido. Conserva la operación y revisa su origen."); }
    }
}
