import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import assert from 'node:assert/strict';

// Read-only review of source contracts. No network, browser, or database access.
const root = process.cwd();
const read = p => fs.readFileSync(path.join(root, p), 'utf8');
const seed = read('src/SistemiPos.Infrastructure/Security/SecurityBootstrapper.cs');
const roleSource = read('src/SistemiPos.Application/Security/SecurityRoleNames.cs');
const permissionSource = read('src/SistemiPos.Application/Security/PermissionCodes.cs');
const roleNames = Object.fromEntries([...roleSource.matchAll(/public const string (\w+) = "([^"]+)"/g)].map(m => [m[1], m[2]]));
const codes = Object.fromEntries([...permissionSource.matchAll(/public const string (\w+) = "([^"]+)"/g)].map(m => [m[1], m[2]]));
const roles = {};
for (const m of seed.matchAll(/await EnsureRoleAsync\(\s*SecurityRoleNames\.(\w+),\s*"[^"]*",\s*([\s\S]*?),\s*enforcePermissions:\s*true/g)) {
  roles[m[1]] = m[2].includes('PermissionCodes.All') ? Object.keys(codes) : [...m[2].matchAll(/PermissionCodes\.(\w+)/g)].map(p => p[1]);
}
assert.equal(Object.keys(roles).length, 8);
const checks = [];
const evidence = (id, description, observed) => {
  assert.ok(observed, `Source changed; re-review ${id}`);
  checks.push({ id, description, confirmed: true, method: 'source contract inspection' });
};
evidence('REV-01', 'Restaurant and checkout use different order repositories',
  read('src/SistemiPos.Infrastructure/Restaurant/RestaurantRepository.cs').includes('db.RestaurantOrders') &&
  read('src/SistemiPos.Infrastructure/Orders/OrderRepository.cs').includes('db.Set<RestaurantOrder>()') &&
  read('src/SistemiPos.Infrastructure/Persistence/Configurations/RestaurantOrderConfiguration.cs').includes('ToTable("PosOrders")'));
evidence('REV-02', 'Manager can switch branches in navigation but lacks the controller class permission',
  roles.Manager.includes('BranchSwitch') && !roles.Manager.includes('OrganizationsView') &&
  read('src/SistemiPos.Web/Controllers/OrganizationController.cs').includes('[Authorize(Policy = PermissionCodes.OrganizationsView)]'));
evidence('REV-03', 'Warehouse role lacks ProductsManage, required for physical counts',
  !roles.Warehouse.includes('ProductsManage') &&
  /Authorize\(Policy = PermissionCodes.ProductsManage\)\][\s\S]{0,180}PhysicalCount\(/.test(read('src/SistemiPos.Web/Controllers/InventoryController.cs')));
evidence('REV-04', 'Built-in role permissions are forcibly reapplied during startup',
  seed.includes('RemoveRange(current.Where(x => !desiredIds.Contains(x.PermissionId)))') &&
  read('src/SistemiPos.Web/Program.cs').includes('await bootstrapper.SeedAsync()'));
evidence('REV-05', 'The cash close service accepts an amount when no count exists',
  read('src/SistemiPos.Application/Cash/CashService.cs').includes('LastOrDefault()?.CountedCash ?? request.CountedCash'));
evidence('REV-06', 'The KDS reload guard treats the persistent ready button as busy',
  read('src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml').includes('class="kds-btn kds-btn-done" disabled') &&
  read('src/SistemiPos.Web/wwwroot/js/restaurant.js').includes(".kds-card button:disabled:not(.is-done)"));
evidence('REV-07', 'KDS status permission is computed but never used elsewhere in the view',
  (read('src/SistemiPos.Web/Views/Restaurant/Kitchen.cshtml').match(/\bcanManageStatus\b/g) || []).length === 1);
evidence('REV-08', 'Cashier and POS role receive no closing-report or blind-count flag by default',
  ['Cashier', 'PointOfSale'].every(r => !roles[r].includes('CashClosingReport') && !roles[r].includes('CashCountHideExpected')));

const sourceFiles = [];
function walk(dir) {
  for (const item of fs.readdirSync(dir, { withFileTypes: true })) {
    if (['bin', 'obj', 'Migrations'].includes(item.name)) continue;
    const p = path.join(dir, item.name);
    if (item.isDirectory()) walk(p);
    else if (p.endsWith('.cs')) sourceFiles.push(p);
  }
}
walk(path.join(root, 'src'));
const resolverConsumers = sourceFiles.filter(p => !p.endsWith('EnterpriseSettingsResolver.cs') &&
  !p.endsWith('IEnterpriseSettingsResolver.cs') && !p.endsWith('DependencyInjection.cs') &&
  fs.readFileSync(p, 'utf8').includes('IEnterpriseSettingsResolver'));
evidence('REV-09', 'Enterprise settings resolver has no production consumers outside registration', resolverConsumers.length === 0);

const worker = { self: { addEventListener() {}, clients: { matchAll: async () => [] } }, URL, Response, console };
vm.createContext(worker);
vm.runInContext(read('src/SistemiPos.Web/wwwroot/service-worker.js'), worker);
const sample = { clientOperationId: 'AUDIT-SYNTHETIC', offlineTicket: 'synthetic-ticket', total: 89,
  context: { userId: 'USER-A', branchId: 'BRANCH-A' }, syncStatus: 'Pending', items: [] };
const payload = worker.toSyncRequest(sample);
assert.equal(payload.offlineTicket, undefined);
assert.equal(payload.expectedTotal, undefined);
checks.push({ id: 'REV-10', description: 'Background worker drops offlineTicket and expectedTotal', confirmed: true,
  method: 'execute original worker serializer with synthetic data', actualKeys: Object.keys(payload) });

let request;
worker.getAllPendingSales = async () => [sample];
worker.putPendingSale = async () => {};
worker.fetch = async (url, options) => {
  request = { url, options };
  return { ok: true, json: async () => ({ results: [] }) };
};
await worker.syncPendingSalesInBackground();
assert.equal(request.options.headers.RequestVerificationToken, undefined);
checks.push({ id: 'REV-11', description: 'Background sync sends no antiforgery token', confirmed: true,
  method: 'execute original background sync with mocked transport', headers: request.options.headers });

const cacheData = new Map();
worker.caches = {
  open: async () => ({ put: async (key, response) => cacheData.set(key, response) }),
  match: async key => cacheData.get(key)?.clone()
};
worker.fetch = async () => new Response('SYNTHETIC HTML FOR USER A');
await worker.handleNavigation({ url: 'https://audit.invalid/pos' });
worker.fetch = async () => { throw new Error('synthetic offline'); };
const fallback = await worker.handleNavigation({ url: 'https://audit.invalid/pos' });
assert.equal(await fallback.text(), 'SYNTHETIC HTML FOR USER A');
checks.push({ id: 'REV-12', description: 'Offline HTML cache uses /pos without identity or branch partition', confirmed: true,
  method: 'execute original navigation handler with mocked cache and synthetic HTML', cacheKeys: [...cacheData.keys()] });

const output = {
  date: '2026-10-21', timezone: 'America/Guatemala',
  scope: 'Static contracts and isolated JavaScript simulations; not end-to-end acceptance',
  builtInRoles: Object.entries(roles).map(([key, permissions]) => ({ key, name: roleNames[key], permissionCount: permissions.length,
    permissions: permissions.map(p => codes[p]) })),
  checks, sourceCounts: { csharpFilesExcludingMigrationsAndBuild: sourceFiles.length }
};
fs.writeFileSync(path.join(root, 'artifacts/revision-general/coherence-evidence.json'), JSON.stringify(output, null, 2));
console.log(JSON.stringify({ confirmedContractObservations: checks.length, roles: output.builtInRoles.map(({name,permissionCount}) => ({name,permissionCount})), ...output.sourceCounts }, null, 2));
