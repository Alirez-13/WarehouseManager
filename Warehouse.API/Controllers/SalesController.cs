using Microsoft.AspNetCore.Mvc;
using Warehouse.Core.Commands;
using Warehouse.Core.Interfaces;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public SalesController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpPost("outbound")]
    public async Task<IActionResult> CreateOutboundSale([FromBody] CreateOutboundSaleCommand command,
        CancellationToken ct)
    {
        await _inventoryService.ProcessOutboundSaleAsync(command, ct);
        return Accepted(new { Message = "Outbound sale successfully recorded and stock adjusted." });
    }
}