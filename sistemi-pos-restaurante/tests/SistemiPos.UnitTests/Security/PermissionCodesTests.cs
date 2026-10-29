using SistemiPos.Application.Security;

namespace SistemiPos.UnitTests.Security;

public sealed class PermissionCodesTests
{
    [Fact]
    public void All_Contains_Unique_Codes()
    {
        var codes = PermissionCodes.All.Select(x => x.Code).ToList();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
