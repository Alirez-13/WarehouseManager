using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Core.Commands;
using Warehouse.Core.Entities;
using Warehouse.Tests.Fixtures;
using Xunit;

namespace Warehouse.Tests.Tests;

public class OutboundTransactionTests : IClassFixture<WarehouseTestFactory>
{
    private readonly WarehouseTestFactory _factory;
    private readonly HttpClient _client;

    public OutboundTransactionTests(WarehouseTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OutboundSale_Creates_Customer_And_Deducts_Stock()
    {
        var tenantId = "tenant_sale_" + Guid.NewGuid();
        int productId;

        await using (var context = _factory.CreateTestDbContext(tenantId))
        {
            var product = new Product { Name = "Cement 50kg", Sku = "CEM-50", Unit = "bag" };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;

            context.InventoryItems.Add(new InventoryItem { ProductId = productId, QuantityOnHand = 100 });
            await context.SaveChangesAsync();
        }

        var command = new CreateOutboundSaleCommand(
            InvoiceNumber: "INV-2026-001",
            CustomerName: "John Builders",
            CustomerPhone: "+1555019283",
            CustomerAddress: "42 Industrial Rd",
            Notes: "Net 30",
            Items: new List<OutboundSaleItemCommand>
            {
                new(ProductId: productId, Quantity: 20, UnitPrice: 8.00m)
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sales/outbound")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("X-Tenant-ID", tenantId);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await using (var verificationDb = _factory.CreateTestDbContext(tenantId))
        {
            // Verify stock reduced
            var stock = await verificationDb.InventoryItems.SingleAsync(i => i.ProductId == productId);
            stock.QuantityOnHand.Should().Be(80);

            // Verify customer created
            var customer = await verificationDb.Customers.SingleOrDefaultAsync(c => c.Phone == "+1555019283");
            customer.Should().NotBeNull();
            customer!.Name.Should().Be("John Builders");

            // Verify outbound line snapshot
            var invoice = await verificationDb.OutboundTransactions
                .Include(t => t.Lines)
                .SingleAsync(t => t.InvoiceNumber == "INV-2026-001");

            invoice.TotalAmount.Should().Be(160.00m);
            invoice.CustomerNameSnapshot.Should().Be("John Builders");
            invoice.Lines.First().LineTotal.Should().Be(160.00m);
        }
    }

    [Fact]
    public async Task OutboundSale_Reuses_Existing_Customer_Without_Duplication()
    {
        var tenantId = "tenant_cust_reuse_" + Guid.NewGuid();
        int productId;
        int customerId;

        await using (var context = _factory.CreateTestDbContext(tenantId))
        {
            var product = new Product { Name = "Timber 2x4", Sku = "TMB-24", Unit = "pcs" };
            var customer = new Customer { Name = "Alice Smith", Phone = "+1999888777" };
            context.Products.Add(product);
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            productId = product.Id;
            customerId = customer.Id;

            context.InventoryItems.Add(new InventoryItem { ProductId = productId, QuantityOnHand = 50 });
            await context.SaveChangesAsync();
        }

        var command = new CreateOutboundSaleCommand(
            InvoiceNumber: "INV-REUSE-01",
            CustomerName: "Alice Smith Changed Name", // Even if different name is passed, phone matches
            CustomerPhone: "+1999888777",
            CustomerAddress: null,
            Notes: null,
            Items: new List<OutboundSaleItemCommand> { new(productId, 5, 10m) }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sales/outbound")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("X-Tenant-ID", tenantId);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await using (var verificationDb = _factory.CreateTestDbContext(tenantId))
        {
            var customerCount = await verificationDb.Customers.CountAsync(c => c.Phone == "+1999888777");
            customerCount.Should().Be(1);

            var sale = await verificationDb.OutboundTransactions.SingleAsync(s => s.InvoiceNumber == "INV-REUSE-01");
            sale.CustomerId.Should().Be(customerId);
        }
    }

    [Fact]
    public async Task OutboundSale_With_Insufficient_Stock_Returns_422_UnprocessableEntity_And_Rolls_Back()
    {
        var tenantId = "tenant_insufficient_" + Guid.NewGuid();
        int productId;

        await using (var context = _factory.CreateTestDbContext(tenantId))
        {
            var product = new Product { Name = "Plywood 18mm", Sku = "PLY-18", Unit = "sheet" };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;

            context.InventoryItems.Add(new InventoryItem { ProductId = productId, QuantityOnHand = 5 });
            await context.SaveChangesAsync();
        }

        var command = new CreateOutboundSaleCommand(
            InvoiceNumber: "INV-FAIL-01",
            CustomerName: "Failed Corp",
            CustomerPhone: "+100000000",
            CustomerAddress: null,
            Notes: null,
            Items: new List<OutboundSaleItemCommand>
            {
                new(ProductId: productId, Quantity: 10, UnitPrice: 30.00m) // Requests 10, but only 5 exist
            }
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sales/outbound")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("X-Tenant-ID", tenantId);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Ensure database state rolled back
        await using (var verificationDb = _factory.CreateTestDbContext(tenantId))
        {
            var stock = await verificationDb.InventoryItems.SingleAsync(i => i.ProductId == productId);
            stock.QuantityOnHand.Should().Be(5);

            var invoice = await verificationDb.OutboundTransactions.FirstOrDefaultAsync(i => i.InvoiceNumber == "INV-FAIL-01");
            invoice.Should().BeNull();
        }
    }
}