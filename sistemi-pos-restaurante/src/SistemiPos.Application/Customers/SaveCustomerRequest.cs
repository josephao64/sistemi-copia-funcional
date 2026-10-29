namespace SistemiPos.Application.Customers;

public sealed record SaveCustomerRequest(
    string Name,
    string? Nit,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    int CreditDays = 30,
    int GraceDays = 0,
    string? VisitOrigin = null);
