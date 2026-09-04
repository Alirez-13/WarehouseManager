namespace Warehouse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Core.Commands;
using Core.Interfaces;
using Infrastructure.Services;

[ApiController]
[Route("api/[controller]")]
public class InboundController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InboundController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpPost("receipt")]
    public async Task<IActionResult> CreateInboundReceipt([FromBody] CreateInboundReceiptCommand command,
        CancellationToken ct)
    {
        await _inventoryService.ProcessInboundReceiptAsync(command, ct);
        return Accepted(new { Message = "Inbound batch restocked successfully." });
    }
}