using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Security;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = RoleAccessPolicies.SalesAccess)]
[Authorize(Policy = PermissionCodes.SalesManage)]
public sealed class PosController : Controller
{
    public IActionResult Offline() => View();
}
