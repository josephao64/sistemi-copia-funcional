using SistemiPos.Application.Organizations;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Domain.Organizations;

namespace SistemiPos.UnitTests.Organizations;

public sealed class EnterpriseSettingsResolverTests
{
    private readonly BusinessGroup _group;
    private readonly Company _company1;
    private readonly Company _company2;
    private readonly Branch _branch1A;
    private readonly Branch _branch1B;
    private readonly Branch _branch2A;
    private readonly CompanySetting _company1Setting;

    private readonly FakeOrganizationRepository _orgRepo;
    private readonly FakeCompanySettingsRepository _companySettingsRepo;
    private readonly FakeBranchSettingsRepository _branchSettingsRepo;
    private readonly EnterpriseSettingsResolver _resolver;

    public EnterpriseSettingsResolverTests()
    {
        _group = new BusinessGroup("GRP-01", "Grupo Corporativo");
        _company1 = new Company(_group.Id, "CMP-01", "Restaurante Central", "Restaurante Central S.A.", "111111-1");
        _company2 = new Company(_group.Id, "CMP-02", "Restaurante Norte", "Restaurante Norte S.A.", "222222-2");

        _branch1A = new Branch(_company1.Id, "SUC-01A", "Sucursal Zona 1", establishmentCode: "1");
        _branch1B = new Branch(_company1.Id, "SUC-01B", "Sucursal Zona 10", establishmentCode: "2");
        _branch2A = new Branch(_company2.Id, "SUC-02A", "Sucursal Norte", establishmentCode: "1");

        _company1Setting = new CompanySetting(
            _company1.Id,
            "Restaurante Central",
            businessNit: "111111-1",
            receiptFooter: "¡Gracias por su visita a Restaurante Central!",
            cashPaymentsEnabled: true,
            transferPaymentsEnabled: true,
            posPaymentsEnabled: true,
            creditPaymentsEnabled: true,
            taxPercent: 12m);

        _orgRepo = new FakeOrganizationRepository([_company1, _company2], [_branch1A, _branch1B, _branch2A]);
        _companySettingsRepo = new FakeCompanySettingsRepository([_company1Setting]);
        _branchSettingsRepo = new FakeBranchSettingsRepository();

        _resolver = new EnterpriseSettingsResolver(_orgRepo, _companySettingsRepo, _branchSettingsRepo);
    }

    [Fact]
    public async Task Resolve_CompanyOnly_ReturnsCompanyValuesAndOrigin()
    {
        var effective = await _resolver.ResolveEffectiveSettingsAsync(_company1.Id);

        Assert.Equal(_company1.Id, effective.CompanyId);
        Assert.Null(effective.BranchId);
        Assert.Equal("Restaurante Central", effective.BusinessName.Value);
        Assert.Equal(SettingOrigin.Company, effective.BusinessName.Origin);
        Assert.Equal("¡Gracias por su visita a Restaurante Central!", effective.ReceiptFooter.Value);
        Assert.Equal(SettingOrigin.Company, effective.ReceiptFooter.Origin);
        Assert.True(effective.CashPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Company, effective.CashPaymentsEnabled.Origin);
        Assert.Equal(12m, effective.TaxPercent.Value);
        Assert.Equal(SettingOrigin.Company, effective.TaxPercent.Origin);
    }

    [Fact]
    public async Task Resolve_BranchWithoutOverride_InheritsCompanySettings()
    {
        var effective = await _resolver.ResolveEffectiveSettingsAsync(_company1.Id, _branch1A.Id);

        Assert.Equal(_company1.Id, effective.CompanyId);
        Assert.Equal(_branch1A.Id, effective.BranchId);
        Assert.Equal("¡Gracias por su visita a Restaurante Central!", effective.ReceiptFooter.Value);
        Assert.Equal(SettingOrigin.Company, effective.ReceiptFooter.Origin);
        Assert.True(effective.CashPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Company, effective.CashPaymentsEnabled.Origin);
        Assert.True(effective.TransferPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Company, effective.TransferPaymentsEnabled.Origin);
        Assert.Equal(12m, effective.TaxPercent.Value);
        Assert.Equal(SettingOrigin.Company, effective.TaxPercent.Origin);
    }

    [Fact]
    public async Task Resolve_BranchWithOverride_ReturnsBranchValuesAndBranchOrigin()
    {
        var branchSetting = new BranchSetting(
            _branch1A.Id,
            establishmentCode: "EST-01A",
            establishmentName: "Restaurante Central - Zona 1",
            receiptFooter: "¡Gracias por su visita a Sucursal Zona 1!",
            overridePaymentMethods: true,
            cashPaymentsEnabled: true,
            transferPaymentsEnabled: false,
            posPaymentsEnabled: true,
            creditPaymentsEnabled: false);
        await _branchSettingsRepo.AddAsync(branchSetting);

        var effective = await _resolver.ResolveEffectiveSettingsAsync(_company1.Id, _branch1A.Id);

        Assert.Equal("EST-01A", effective.EstablishmentCode.Value);
        Assert.Equal(SettingOrigin.Branch, effective.EstablishmentCode.Origin);
        Assert.Equal("Restaurante Central - Zona 1", effective.EstablishmentName.Value);
        Assert.Equal(SettingOrigin.Branch, effective.EstablishmentName.Origin);
        Assert.Equal("¡Gracias por su visita a Sucursal Zona 1!", effective.ReceiptFooter.Value);
        Assert.Equal(SettingOrigin.Branch, effective.ReceiptFooter.Origin);

        Assert.True(effective.CashPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Branch, effective.CashPaymentsEnabled.Origin);
        Assert.False(effective.TransferPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Branch, effective.TransferPaymentsEnabled.Origin);
        Assert.True(effective.PosPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Branch, effective.PosPaymentsEnabled.Origin);
        Assert.False(effective.CreditPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Branch, effective.CreditPaymentsEnabled.Origin);

        // Company level settings remain Company origin
        Assert.Equal(12m, effective.TaxPercent.Value);
        Assert.Equal(SettingOrigin.Company, effective.TaxPercent.Origin);
        Assert.Equal("GTQ", effective.CurrencyCode.Value);
        Assert.Equal(SettingOrigin.Company, effective.CurrencyCode.Origin);
    }

    [Fact]
    public async Task Resolve_DifferentBranches_AreIsolated()
    {
        var branchSettingA = new BranchSetting(
            _branch1A.Id,
            receiptFooter: "Pie exclusivo de Sucursal A",
            overridePaymentMethods: true,
            cashPaymentsEnabled: true,
            transferPaymentsEnabled: false,
            posPaymentsEnabled: false,
            creditPaymentsEnabled: false);
        await _branchSettingsRepo.AddAsync(branchSettingA);

        // Branch 1B does not have an override
        var effectiveB = await _resolver.ResolveEffectiveSettingsAsync(_company1.Id, _branch1B.Id);

        Assert.Equal("¡Gracias por su visita a Restaurante Central!", effectiveB.ReceiptFooter.Value);
        Assert.Equal(SettingOrigin.Company, effectiveB.ReceiptFooter.Origin);
        Assert.True(effectiveB.TransferPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Company, effectiveB.TransferPaymentsEnabled.Origin);
        Assert.True(effectiveB.PosPaymentsEnabled.Value);
        Assert.Equal(SettingOrigin.Company, effectiveB.PosPaymentsEnabled.Origin);
    }

    [Fact]
    public async Task Resolve_CrossCompanyBranch_ThrowsDomainRuleException()
    {
        // _branch2A belongs to _company2, but we query with _company1
        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            _resolver.ResolveEffectiveSettingsAsync(_company1.Id, _branch2A.Id));

        Assert.Contains("no pertenece a la empresa", ex.Message);
    }

    [Fact]
    public async Task Resolve_NonExistentCompany_ThrowsDomainRuleException()
    {
        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            _resolver.ResolveEffectiveSettingsAsync(Guid.NewGuid()));

        Assert.Contains("La empresa especificada no existe", ex.Message);
    }

    [Fact]
    public async Task SaveBranchSettingAsync_CreatesAndUpdatesOverride()
    {
        var request = new SaveBranchSettingRequest(
            EstablishmentCode: "10",
            EstablishmentName: "Zona 10 Express",
            ReceiptFooter: "Visítenos pronto",
            OverridePaymentMethods: true,
            CashPaymentsEnabled: true,
            TransferPaymentsEnabled: true,
            PosPaymentsEnabled: true,
            CreditPaymentsEnabled: false);

        await _resolver.SaveBranchSettingAsync(_company1.Id, _branch1B.Id, request);

        var saved = await _resolver.GetBranchSettingAsync(_branch1B.Id);
        Assert.NotNull(saved);
        Assert.Equal("10", saved.EstablishmentCode);
        Assert.Equal("Zona 10 Express", saved.EstablishmentName);
        Assert.Equal("Visítenos pronto", saved.ReceiptFooter);
        Assert.True(saved.OverridePaymentMethods);
        Assert.False(saved.CreditPaymentsEnabled);

        // Update existing
        var updateRequest = new SaveBranchSettingRequest(
            EstablishmentCode: "10-B",
            EstablishmentName: "Zona 10 Express Modificada",
            ReceiptFooter: "Nuevo Pie",
            OverridePaymentMethods: false,
            CashPaymentsEnabled: true,
            TransferPaymentsEnabled: true,
            PosPaymentsEnabled: true,
            CreditPaymentsEnabled: true);

        await _resolver.SaveBranchSettingAsync(_company1.Id, _branch1B.Id, updateRequest);

        var updated = await _resolver.GetBranchSettingAsync(_branch1B.Id);
        Assert.NotNull(updated);
        Assert.Equal("10-B", updated.EstablishmentCode);
        Assert.False(updated.OverridePaymentMethods);
    }

    [Fact]
    public async Task SaveBranchSettingAsync_CrossCompany_ThrowsDomainRuleException()
    {
        var request = new SaveBranchSettingRequest("1", "Nombre", "Pie", false, true, true, true, true);

        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            _resolver.SaveBranchSettingAsync(_company1.Id, _branch2A.Id, request));

        Assert.Contains("no pertenece a la empresa", ex.Message);
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        private readonly List<Company> _companies;
        private readonly List<Branch> _branches;

        public FakeOrganizationRepository(IEnumerable<Company> companies, IEnumerable<Branch> branches)
        {
            _companies = companies.ToList();
            _branches = branches.ToList();
        }

        public Task<List<BusinessGroup>> GetBusinessGroupsAsync(bool onlyActive = false, CancellationToken cancellationToken = default) => Task.FromResult(new List<BusinessGroup>());
        public Task<BusinessGroup?> GetBusinessGroupByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<BusinessGroup?>(null);
        public Task<BusinessGroup?> GetBusinessGroupByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult<BusinessGroup?>(null);
        public Task AddBusinessGroupAsync(BusinessGroup group, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<Company>> GetCompaniesAsync(Guid? businessGroupId = null, bool onlyActive = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companies.Where(x => !businessGroupId.HasValue || x.BusinessGroupId == businessGroupId.Value).ToList());
        public Task<Company?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companies.FirstOrDefault(x => x.Id == id));
        public Task<Company?> GetCompanyByCodeAsync(Guid businessGroupId, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_companies.FirstOrDefault(x => x.BusinessGroupId == businessGroupId && x.Code == code));
        public Task AddCompanyAsync(Company company, CancellationToken cancellationToken = default) { _companies.Add(company); return Task.CompletedTask; }

        public Task<List<Branch>> GetBranchesAsync(Guid? companyId = null, bool onlyActive = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(_branches.Where(x => !companyId.HasValue || x.CompanyId == companyId.Value).ToList());
        public Task<Branch?> GetBranchByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_branches.FirstOrDefault(x => x.Id == id));
        public Task<Branch?> GetBranchByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(_branches.FirstOrDefault(x => x.CompanyId == companyId && x.Code == code));
        public Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default) { _branches.Add(branch); return Task.CompletedTask; }

        public Task<List<Warehouse>> GetWarehousesAsync(Guid? branchId = null, bool onlyActive = false, CancellationToken cancellationToken = default) => Task.FromResult(new List<Warehouse>());
        public Task<Warehouse?> GetWarehouseByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Warehouse?>(null);
        public Task<Warehouse?> GetWarehouseByCodeAsync(Guid branchId, string code, CancellationToken cancellationToken = default) => Task.FromResult<Warehouse?>(null);
        public Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<UserScopeAssignment>> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new List<UserScopeAssignment>());
        public Task<List<Branch>> GetAuthorizedBranchesForUserAsync(Guid userId, bool isSuperAdmin, CancellationToken cancellationToken = default) => Task.FromResult(_branches);
        public Task SaveUserScopesAsync(Guid userId, IEnumerable<UserScopeAssignment> assignments, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCompanySettingsRepository : ICompanySettingsRepository
    {
        private readonly List<CompanySetting> _settings;

        public FakeCompanySettingsRepository(IEnumerable<CompanySetting>? settings = null)
        {
            _settings = settings?.ToList() ?? [];
        }

        public Task<CompanySetting?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings.FirstOrDefault(x => x.CompanyId == companyId));

        public Task AddAsync(CompanySetting setting, CancellationToken cancellationToken = default)
        {
            _settings.Add(setting);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CompanySetting setting, CancellationToken cancellationToken = default)
        {
            var idx = _settings.FindIndex(x => x.CompanyId == setting.CompanyId);
            if (idx >= 0) _settings[idx] = setting;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBranchSettingsRepository : IBranchSettingsRepository
    {
        private readonly List<BranchSetting> _settings = [];

        public Task<BranchSetting?> GetByBranchIdAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings.FirstOrDefault(x => x.BranchId == branchId));

        public Task AddAsync(BranchSetting setting, CancellationToken cancellationToken = default)
        {
            _settings.Add(setting);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BranchSetting setting, CancellationToken cancellationToken = default)
        {
            var idx = _settings.FindIndex(x => x.BranchId == setting.BranchId);
            if (idx >= 0) _settings[idx] = setting;
            return Task.CompletedTask;
        }
    }
}
