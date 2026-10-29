namespace SistemiPos.Application.Abstractions;

public interface ICurrentUserAccessor
{
    string UserName { get; }
    Guid? UserId => null;
}
