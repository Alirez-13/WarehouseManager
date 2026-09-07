using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Core.Commands;
using Warehouse.Core.Entities;
using Warehouse.Tests.Fixtures;
using Xunit;

namespace Warehouse.Tests.Tests;

public class InboundTransactionTests : IClassFixture<WarehouseTestFactory>
{
    private readonly WarehouseTestFactory _factory;
    private readonly HttpClient _client;

    public InboundTransactionTests(WarehouseTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task InboundReceipt_Increments_Stock_And_Persists_Immutable_Snapshots()
    {
        var tenantId = "tenant_inbound_" + Guid.NewGuid();
        int productId;

        // Seed product
        await using (var context = _factory.CreateTestDbContext(tenantId))
        {
            var product = new Product { Name = "Steel Bolt M8", Sku = "BOLT-M8", Unit = "pack" };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;
        }

        var command = new CreateInboundReceiptCommand(
            ReferenceNumber: "PO-99124",
            SupplierName: "Fastener Supply Co",
            Notes: "Dock 4 delivery",
            Items: new List<InboundReceiptItemCommand>
            {
                new(ProductId: productId, Quantity: 50, UnitPrice: 12.50m)
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inbound/receipt")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("X-Tenant-ID", tenantId);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Verify DB State
        await using (var verificationDb = _factory.CreateTestDbContext(tenantId))
        {
            var stock = await verificationDb.InventoryItems.SingleAsync(i => i.ProductId == productId);
            stock.QuantityOnHand.Should().Be(50);

            var inboundTx = await verificationDb.InboundTransactions
                .Include(t => t.Lines)
                .SingleAsync(t => t.ReferenceNumber == "PO-99124");

            inboundTx.TotalAmount.Should().Be(625.00m); // 50 * 12.50
            inboundTx.Lines.Should().HaveCount(1);

            var line = inboundTx.Lines.First();
            line.ProductNameSnapshot.Should().Be("Steel Bolt M8");
            line.ProductSkuSnapshot.Should().Be("BOLT-M8");
            line.UnitSnapshot.Should().Be("pack");
            line.Quantity.Should().Be(50);
            line.UnitPrice.Should().Be(12.50m);
            line.LineTotal.Should().Be(625.00m);
        }
    }
}