using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemiPos.Application.Customers;
using SistemiPos.Application.Security;
using SistemiPos.Domain.Common.Exceptions;
using SistemiPos.Web.Models.Customers;

namespace SistemiPos.Web.Controllers;

[Authorize(Policy = PermissionCodes.CustomersView)]
public sealed class CustomersController(ICustomerService customerService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var customers = await customerService.GetCustomersAsync(cancellationToken);
        return View(customers);
    }

    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetCustomerAsync(id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        ViewData["CustomerName"] = customer.Name;
        var history = await customerService.GetHistoryAsync(id, cancellationToken);
        return View(history);
    }

    [Authorize(Policy = PermissionCodes.CustomersManage)]
    [HttpGet]
    public IActionResult Create() => View("Form", new CustomerFormViewModel());

    [Authorize(Policy = PermissionCodes.CustomersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        try
        {
            await customerService.CreateCustomerAsync(Map(model), cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
    }

    [Authorize(Policy = PermissionCodes.CustomersManage)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerService.GetCustomerAsync(id, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        return View("Form", new CustomerFormViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Nit = customer.Nit,
            Phone = customer.Phone,
            VisitOrigin = customer.VisitOrigin,
            Email = customer.Email,
            Address = customer.Address,
            CreditDays = customer.CreditDays,
            GraceDays = customer.GraceDays,
            IsActive = customer.IsActive
        });
    }

    [Authorize(Policy = PermissionCodes.CustomersManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id is null)
        {
            return View("Form", model);
        }

        try
        {
            await customerService.UpdateCustomerAsync(model.Id.Value, Map(model), cancellationToken);
            return RedirectToAction(nameof(Index));
        }
        catch (DomainRuleException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", model);
        }
    }

    private static SaveCustomerRequest Map(CustomerFormViewModel model) =>
        new(model.Name, model.Nit, model.Phone, model.Email, model.Address, model.IsActive, model.CreditDays, model.GraceDays, model.VisitOrigin);
}
