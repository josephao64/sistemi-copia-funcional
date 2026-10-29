namespace SistemiPos.Application.Customers;

public enum NitLookupSource
{
    Local,
    Provider,
    Manual
}

public sealed record NitLookupResult(
    string Nit,
    string? Name,
    string? Address,
    Guid? CustomerId,
    NitLookupSource Source,
    string Message)
{
    public bool Found => Source != NitLookupSource.Manual && !string.IsNullOrWhiteSpace(Name);
}

public sealed record NitProviderLookupResult(string Nit, string Name, string? Address = null);
