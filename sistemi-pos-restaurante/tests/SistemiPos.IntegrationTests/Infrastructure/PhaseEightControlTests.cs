using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SistemiPos.Application.Abstractions;
using SistemiPos.Application.Organizations;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Audit;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;
using SistemiPos.Domain.Sales;
using SistemiPos.Infrastructure.Exports;
using SistemiPos.Infrastructure.Persistence;
using SistemiPos.Infrastructure.Reports;
using SistemiPos.Infrastructure.Sales;

namespace SistemiPos.IntegrationTests.Infrastructure;

// Component tests only: no database connection, web host, certifier or backup executable is started.
public sealed class PhaseEightControlTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=PhaseEightNoConnection;Username=unused;Password=unused;Timeout=1").Options);

    private static ControlReportService Reports(AppDbContext db, Scope scope, params string[] permissions) =>
        new(db, scope, new User(), new Authorization(permissions), new Clock(), new ExcelExportService(), new TablePdfWriter());

    [Fact]
    public async Task Reports_require_view_permission_before_querying_data()
    {
        using var db=Database();
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,new Scope()).GetAsync(new()));
        Assert.Contains("permiso",ex.Message);
    }

    [Theory]
    [InlineData("excel")]
    [InlineData("pdf")]
    public async Task Exports_require_export_permission_even_when_viewing_is_allowed(string format)
    {
        using var db=Database();var service=Reports(db,new Scope(),PermissionCodes.ReportsView);
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>format=="excel"
            ? service.ExportExcelAsync(new()) : service.ExportPdfAsync(new()));
        Assert.Contains("permiso",ex.Message);
    }

    [Fact]
    public async Task Audit_requires_its_own_permission()
    {
        using var db=Database();
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,new Scope(),PermissionCodes.ReportsView).GetAsync(new(Report:"audit")));
        Assert.Contains("permiso",ex.Message);
    }

    [Fact]
    public async Task An_unassigned_branch_is_rejected_even_with_consolidation_permission()
    {
        using var db=Database();
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,new Scope(),PermissionCodes.ReportsView,PermissionCodes.ReportsConsolidate)
            .GetAsync(new(BranchId:Guid.NewGuid())));
        Assert.Contains("sucursal",ex.Message);
    }

    [Fact]
    public async Task An_unassigned_company_is_rejected()
    {
        using var db=Database();
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,new Scope(),PermissionCodes.ReportsView,PermissionCodes.ReportsConsolidate)
            .GetAsync(new(CompanyId:Guid.NewGuid())));
        Assert.Contains("empresa",ex.Message);
    }

    [Fact]
    public async Task An_assigned_other_branch_still_requires_consolidation_permission()
    {
        using var db=Database();var scope=new Scope();
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,scope,PermissionCodes.ReportsView)
            .GetAsync(new(BranchId:scope.OtherBranch)));
        Assert.Contains("consolidación",ex.Message);
    }

    [Fact]
    public async Task A_missing_active_branch_does_not_grant_global_access()
    {
        using var db=Database();var scope=new Scope {CurrentBranchId=null};
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,scope,PermissionCodes.ReportsView).GetAsync(new()));
        Assert.Contains("autorizada",ex.Message);
    }

    [Theory]
    [InlineData("2026-10-21","2026-10-20")]
    [InlineData("2024-01-01","2026-10-21")]
    public async Task Invalid_date_ranges_are_rejected_before_querying(string from,string to)
    {
        using var db=Database();
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>Reports(db,new Scope(),PermissionCodes.ReportsView)
            .GetAsync(new(From:DateOnly.Parse(from),To:DateOnly.Parse(to))));
        Assert.Contains("rango",ex.Message);
    }

    [Fact]
    public async Task Fiscal_management_requires_permission_before_loading_sales()
    {
        using var db=Database();var service=new FiscalService(db,new Scope(),new User(),new Authorization([]),new UnconfiguredFiscalProvider());
        await Assert.ThrowsAsync<DomainRuleException>(()=>service.ListAsync());
        await Assert.ThrowsAsync<DomainRuleException>(()=>service.PrepareAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task An_unconfigured_provider_cannot_invent_a_certification()
    {
        var provider=new UnconfiguredFiscalProvider();Assert.False(provider.IsConfigured);
        await Assert.ThrowsAsync<DomainRuleException>(()=>provider.CertifyAsync("{}","test-key",CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_public_backup_configuration_cannot_start_a_backup(bool publicFolder)
    {
        var values=new Dictionary<string,string?>();
        if(publicFolder)
        {
            values["Backups:Directory"]=Path.Combine(Path.GetTempPath(),"phase8","wwwroot","backups");
            values["Backups:PgDumpPath"]=Environment.ProcessPath;
        }
        var service=new PostgresBackupService(new ConfigurationBuilder().AddInMemoryCollection(values).Build(),new HostEnvironment());
        var status=await service.StatusAsync();Assert.False(status.Configured);Assert.False(status.AutomaticEnabled);
        Assert.Empty(status.Files);
        await Assert.ThrowsAsync<DomainRuleException>(()=>service.CreateAsync());
    }

    [Fact]
    public async Task Backup_intervals_and_retention_are_bounded_without_running_tools()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Backups:IntervalHours"]="0",["Backups:RetentionDays"]="1000" }).Build();
        var status=await new PostgresBackupService(config,new HostEnvironment()).StatusAsync();
        Assert.Equal(1,status.IntervalHours);Assert.Equal(365,status.RetentionDays);
        Assert.Null(status.RpoMinutes);Assert.Null(status.RtoMinutes);
    }

    [Fact]
    public void Pdf_has_valid_offsets_escaped_text_and_repeated_headers()
    {
        var rows=Enumerable.Range(1,100).Select(i=>(IReadOnlyList<string>)new[]{"Pedido ("+i+")", "Q 12.50"}).ToList();
        var bytes=new TablePdfWriter().Write("Ventas","Sucursal Central",["Documento","Total"],rows);
        var text=Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.4",text);Assert.EndsWith("%%EOF",text);
        Assert.Contains("Pedido \\(1\\)",text);Assert.True(Regex.Matches(text,@"/Type /Page /Parent").Count>1);
        Assert.True(Regex.Matches(text,@"\(Documento\)").Count>1);
        var start=int.Parse(Regex.Match(text,@"startxref\n(\d+)").Groups[1].Value);
        Assert.Equal("xref",text.Substring(start,4));
        var offsets=Regex.Matches(text,@"(?m)^(\d{10}) 00000 n");
        for(var i=0;i<offsets.Count;i++)
        {
            var offset=int.Parse(offsets[i].Groups[1].Value);
            Assert.StartsWith($"{i+1} 0 obj",text[offset..]);
        }
    }

    [Fact]
    public void Empty_pdf_has_an_explicit_no_records_message()
    {
        var text=Encoding.Latin1.GetString(new TablePdfWriter().Write("Control","Sucursal Central",["Documento"],[]));
        Assert.Contains("Sin registros para estos filtros.",text);
    }

    [Fact]
    public void Audit_records_context_and_redacts_new_credentials()
    {
        using var db=Database();var scope=new Scope();
        var company=new CompanySetting(scope.CurrentCompanyId!.Value,"Empresa de prueba");
        db.Add(company);db.Entry(company).Property("InfileApiKeyProtected").CurrentValue="phase8-test-credential";
        using var services=new ServiceCollection().AddSingleton<IOperationContextAccessor>(scope).BuildServiceProvider();
        var type=typeof(AppDbContext).Assembly.GetType("SistemiPos.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor")!;
        var interceptor=Activator.CreateInstance(type,new User(),new Clock(),services)!;
        type.GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(interceptor,[db]);
        var audit=Assert.Single(db.ChangeTracker.Entries<AuditLog>()).Entity;
        Assert.Equal(scope.CurrentBranchId,audit.BranchId);Assert.Equal(scope.CurrentCompanyId,audit.CompanyId);
        Assert.Equal(scope.CurrentWarehouseId,audit.WarehouseId);Assert.Equal("phase8-tester",audit.UserName);
        Assert.DoesNotContain("phase8-test-credential",audit.ChangesJson!);Assert.Contains("[PROTEGIDO]",audit.ChangesJson!);
    }

    [Fact]
    public async Task Audit_history_cannot_be_deleted()
    {
        using var db=Database();var audit=new AuditLog();db.Attach(audit);db.Remove(audit);
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>db.SaveChangesAsync());
        Assert.Contains("auditoría",ex.Message);
    }

    [Fact]
    public async Task Fiscal_payload_cannot_be_rewritten()
    {
        using var db=Database();var submission=new FiscalSubmission(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"{}");
        db.Attach(submission);db.Entry(submission).Property(x=>x.PayloadJson).CurrentValue="{\"changed\":true}";
        var ex=await Assert.ThrowsAsync<DomainRuleException>(()=>db.SaveChangesAsync());
        Assert.Contains("contenido original",ex.Message);
    }

    [Fact]
    public void Schema_matches_migration_and_preserves_six_decimal_costs()
    {
        using var db=Database();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("numeric(22,6)",db.Model.FindEntityType(typeof(SaleItem))!.FindProperty("ConsumedUnitCostAtSale")!.GetColumnType());
        Assert.Equal(6,db.Model.FindEntityType(typeof(SistemiPos.Domain.Orders.RestaurantOrderLine))!.FindProperty("CapturedConsumptionCost")!.GetScale());
    }

    private sealed class User : ICurrentUserAccessor { public string UserName=>"phase8-tester";public Guid? UserId=>Guid.Parse("80c3d593-b745-49ee-af60-543a6e85fc85"); }
    private sealed class Clock : IDateTimeProvider { public DateTimeOffset UtcNow=>new(2026,10,21,18,0,0,TimeSpan.Zero); }
    private sealed class Authorization(string[] permissions) : IOperationAuthorizationService
    {
        public Task<bool> HasPermissionAsync(string userName,string permissionCode,CancellationToken cancellationToken=default)=>Task.FromResult(permissions.Contains(permissionCode));
        public Task<string> ValidateCredentialsAsync(string userName,string password,string? requiredPermission=null,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
    private sealed class Scope : IOperationContextAccessor
    {
        public Guid? CurrentBranchId {get;set;}=Guid.NewGuid();public Guid OtherBranch {get;}=Guid.NewGuid();
        public Guid? CurrentCompanyId {get;}=Guid.NewGuid();public Guid? CurrentBusinessGroupId {get;}=Guid.NewGuid();
        public Guid? CurrentWarehouseId {get;}=Guid.NewGuid();public string? CurrentBranchName=>"Central";public string? CurrentCompanyName=>"Empresa de prueba";
        public Task<IReadOnlyList<BranchDto>> GetAuthorizedBranchesAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<BranchDto>>([
            new(CurrentBranchId??Guid.NewGuid(),CurrentCompanyId!.Value,"Empresa de prueba","CEN","Central",null,null,"America/Guatemala",null,true,1),
            new(OtherBranch,CurrentCompanyId!.Value,"Empresa de prueba","NOR","Norte",null,null,"America/Guatemala",null,true,1)]);
        public Task SetCurrentBranchAsync(Guid branchId,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName {get;set;}="Testing";public string ApplicationName {get;set;}="PhaseEightTests";
        public string ContentRootPath {get;set;}=Path.Combine(Path.GetTempPath(),"phase8-content");
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
