using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Security;
using SistemiPos.Web.Models.Security;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.SecurityAuditView)]
public sealed class SecurityAuditController(ISecurityAuditService auditService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var logs = await auditService.GetRecentSecurityLogsAsync(100);
        var viewModel = logs.Select(SecurityAuditItemViewModel.FromEntry).ToList();
        return View(viewModel);
    }
}

