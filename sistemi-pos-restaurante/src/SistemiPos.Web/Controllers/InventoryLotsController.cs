using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Inventory;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.InventoryView)]
public sealed class InventoryLotsController(IInventoryLotService service) : Controller
{
    public async Task<IActionResult> Index(Guid? productId, Guid? warehouseId, CancellationToken cancellationToken)
    {
        if (!productId.HasValue || productId.Value == Guid.Empty || !warehouseId.HasValue || warehouseId.Value == Guid.Empty)
        {
            TempData["Info"] = "Para gestionar los lotes de un producto, haz clic en 'Ver lotes' en la fila del producto correspondiente desde Inventario o consulta Productos por caducar.";
            return RedirectToAction("Index", "Inventory");
        }

        return View(await service.GetAsync(productId.Value, warehouseId.Value, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryAdjust)]
    public async Task<IActionResult> Opening(Guid productId, Guid warehouseId, decimal quantity,
        DateOnly? expiryDate, string? lotNumber, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisa la cantidad y fecha del lote.";
        else
        {
            try
            {
                await service.AddOpeningAsync(productId, warehouseId, quantity, expiryDate, lotNumber, cancellationToken);
                TempData["Success"] = "Saldo inicial asignado al lote. La existencia total no cambió.";
            }
            catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        }
        return RedirectToAction(nameof(Index), new { productId, warehouseId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = PermissionCodes.InventoryAdjust)]
    public async Task<IActionResult> Discard(Guid productId, Guid warehouseId, Guid lotId,
        decimal quantity, string reason, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Revisa los datos de la baja.";
        else
        {
            try
            {
                await service.DiscardAsync(productId, warehouseId, lotId, quantity, reason, cancellationToken);
                TempData["Success"] = "Baja registrada en inventario y en el lote seleccionado.";
            }
            catch (DomainRuleException ex) { TempData["Error"] = ex.Message; }
        }
        return RedirectToAction(nameof(Index), new { productId, warehouseId });
    }
}
