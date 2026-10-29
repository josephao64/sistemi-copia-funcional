using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.UnitTests.Domain;

public sealed class DomainRuleExceptionTests
{
    [Fact]
    public void Constructor_Preserves_Message()
    {
        var exception = new DomainRuleException("Inventario insuficiente.");

        Assert.Equal("Inventario insuficiente.", exception.Message);
    }
}
