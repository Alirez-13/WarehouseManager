namespace Warehouse.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Core.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var customers = await _customerService.SearchCustomersAsync(q, limit, ct);
        return Ok(customers.Select(c => new
        {
            c.Id,
            c.Name,
            c.Phone,
            c.Email,
            c.Address
        }));
    }
}