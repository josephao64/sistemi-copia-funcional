using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Security;
using SistemiPos.Infrastructure;
using SistemiPos.Web.Middleware;
using SistemiPos.Web.Security;
using SistemiPos.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var applicationTechnicalName = builder.Configuration["Application:TechnicalName"] ?? "SistemiPosRestaurante";
var authenticationCookieName = builder.Configuration["Application:AuthenticationCookieName"] ?? ".SistemiPosRestaurante.Auth";
var authenticationCookieHours = builder.Configuration.GetValue(
    "Security:AuthenticationCookieHours",
    (int)LoginSecurityDefaults.AuthenticationCookieLifetime.TotalHours);
var storageRootPath = builder.Configuration["Storage:RootPath"];
if (string.IsNullOrWhiteSpace(storageRootPath))
{
    storageRootPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SISTEMI",
        "SISTEMI POS Restaurante");
}

var storagePaths = new ApplicationStoragePaths(storageRootPath);
Directory.CreateDirectory(storagePaths.DataProtectionKeysPath);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
builder.Services.AddScoped<IOperationContextAccessor, HttpOperationContextAccessor>();
builder.Services.AddScoped<INavigationPermissionService, NavigationPermissionService>();
builder.Services.AddSingleton(storagePaths);
builder.Services.AddDataProtection()
    .SetApplicationName(applicationTechnicalName)
    .PersistKeysToFileSystem(new DirectoryInfo(storagePaths.DataProtectionKeysPath));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = authenticationCookieName;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(Math.Max(1, authenticationCookieHours));
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = CookieAuthenticationSessionExpired.OnRedirectToLogin;
    options.Events.OnValidatePrincipal = async context =>
    {
        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<SistemiPos.Infrastructure.Security.AppUser>>();
        var user = context.Principal is null
            ? null
            : await userManager.GetUserAsync(context.Principal);
        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});
builder.Services.AddAntiforgery(options =>
    options.Cookie.Name = ".SistemiPosRestaurante.Antiforgery");
builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<SaleFlowTimingFilter>();
        var messages = options.ModelBindingMessageProvider;
        messages.SetValueIsInvalidAccessor(value => $"El valor '{value}' no es válido.");
        messages.SetValueMustBeANumberAccessor(_ => "Ingresa un número válido.");
        messages.SetAttemptedValueIsInvalidAccessor((value, _) => $"El valor '{value}' no es válido.");
        messages.SetMissingBindRequiredValueAccessor(_ => "Este dato es obligatorio.");
        messages.SetMissingKeyOrValueAccessor(() => "Falta un dato obligatorio.");
        messages.SetUnknownValueIsInvalidAccessor(_ => "El valor seleccionado no es válido.");
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();

var app = builder.Build();

Directory.CreateDirectory(storagePaths.UploadsPath);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SistemiPos.Web.Middleware.SaleFlowTimingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storagePaths.UploadsPath),
    RequestPath = "/uploads"
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapStaticAssets().AllowAnonymous();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SistemiPos.Infrastructure.Persistence.AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var bootstrapper = scope.ServiceProvider.GetRequiredService<SistemiPos.Infrastructure.Security.SecurityBootstrapper>();
    await bootstrapper.SeedAsync();

    var organizationDefaults = scope.ServiceProvider.GetRequiredService<SistemiPos.Infrastructure.Seed.OrganizationDefaultsSeeder>();
    await organizationDefaults.SeedAsync();

    var catalogDefaults = scope.ServiceProvider.GetRequiredService<SistemiPos.Infrastructure.Seed.CatalogDefaultsSeeder>();
    await catalogDefaults.SeedAsync();

    var demoDataSeeder = scope.ServiceProvider.GetRequiredService<SistemiPos.Infrastructure.Seed.DemoDataSeeder>();
    await demoDataSeeder.SeedAsync();
}

app.Run();

public partial class Program;
