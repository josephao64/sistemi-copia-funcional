using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SistemiPos.FunctionalTests.Infrastructure;

public sealed class TestingWebApplicationFactory : WebApplicationFactory<Program>
{
    static TestingWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Host=127.0.0.1;Port=1;Database=SistemiPosFunctionalTests;Username=unused;Password=unused");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
