namespace SistemiPos.Application.Common.Exceptions;

public sealed class NotFoundException(string message) : ApplicationExceptionBase(message);
