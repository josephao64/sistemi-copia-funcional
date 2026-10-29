using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Reports;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.ReportsView)]
[Route("control/reportes")]
public sealed class ControlReportsController(IControlReportService reports,
    SistemiPos.Application.Catalog.ICatalogService catalog, SistemiPos.Application.Suppliers.ISupplierService suppliers) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ControlQuery query, CancellationToken ct)
    {
        try
        {
            var report=await reports.GetAsync(query,ct);
            ViewBag.Products=(await catalog.SearchProductsAsync(null,ct)).Where(x=>!x.ManagesVariants)
                .Select(x=>new { x.Id, Name=x.DisplayName??x.Name }).ToList();
            ViewBag.Suppliers=(await suppliers.GetSuppliersAsync(ct)).Select(x=>new {x.Id,Name=x.TradeName??x.LegalName}).ToList();
            return View(report);
        }
        catch (DomainRuleException ex) { return BadRequest(ex.Message); }
    }
    [HttpGet("excel"), Authorize(Policy = PermissionCodes.ReportsExport)]
    public async Task<IActionResult> Excel([FromQuery] ControlQuery query, CancellationToken ct)
    {
        try { return File(await reports.ExportExcelAsync(query, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Control.xlsx"); }
        catch (DomainRuleException ex) { return BadRequest(ex.Message); }
    }
    [HttpGet("pdf"), Authorize(Policy = PermissionCodes.ReportsExport)]
    public async Task<IActionResult> Pdf([FromQuery] ControlQuery query, CancellationToken ct)
    {
        try { return File(await reports.ExportPdfAsync(query, ct), "application/pdf", "Control.pdf"); }
        catch (DomainRuleException ex) { return BadRequest(ex.Message); }
    }
}
