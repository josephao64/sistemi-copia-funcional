using System.Net;
using SistemiPos.FunctionalTests.Infrastructure;

namespace SistemiPos.FunctionalTests.Home;

public sealed class HomePageTests : IClassFixture<TestingWebApplicationFactory>
{
    private readonly TestingWebApplicationFactory _factory;

    public HomePageTests(TestingWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Index_Returns_Success_And_Project_Title()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Iniciar sesión", content);
    }
}
