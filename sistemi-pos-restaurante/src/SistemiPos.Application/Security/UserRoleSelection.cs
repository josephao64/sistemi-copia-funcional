namespace SistemiPos.Application.Security;

public static class UserRoleSelection
{
    public const string ValidationMessage = "Debe seleccionar exactamente un rol por usuario.";

    public static IReadOnlyList<string> Resolve(IEnumerable<string> selectedRoles, string? roleName) =>
        selectedRoles.Append(roleName ?? string.Empty)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static bool IsValid(IReadOnlyCollection<string> roles) => roles.Count == 1;
}
