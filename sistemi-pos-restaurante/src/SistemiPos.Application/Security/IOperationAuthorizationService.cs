namespace SistemiPos.Application.Security;

public interface IOperationAuthorizationService
{
    Task<bool> HasPermissionAsync(string userName, string permissionCode, CancellationToken cancellationToken = default);
    Task<string> ValidateCredentialsAsync(string userName, string password, string? requiredPermission = null, CancellationToken cancellationToken = default);
}
