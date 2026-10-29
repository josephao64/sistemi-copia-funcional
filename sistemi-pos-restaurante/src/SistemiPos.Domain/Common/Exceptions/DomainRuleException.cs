namespace SistemiPos.Domain.Common.Exceptions;

public sealed class DomainRuleException(string message) : Exception(message);
