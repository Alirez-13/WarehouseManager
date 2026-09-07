using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Core.Commands;
using Warehouse.Core.Entities;
using Warehouse.Tests.Fixtures;
using Xunit;

namespace Warehouse.Tests.Tests;

public class TenantIsolationTests : IClassFixture<WarehouseTestFactory>
{
    private readonly WarehouseTestFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationTests(WarehouseTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_Without_Tenant_Header_Returns_400_BadRequest()
    {
        var command =
            new CreateInboundReceiptCommand("REF-001", "Vendor A", null, new List<InboundReceiptItemCommand>());

        var response = await _client.PostAsJsonAsync("/api/inbound/receipt", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Data_Written_In_Tenant_A_Is_Completely_Inaccessible_In_Tenant_B()
    {
        var tenantA = "tenant_alpha_" + Guid.NewGuid();
        var tenantB = "tenant_beta_" + Guid.NewGuid();

        // 1. Seed product in Tenant A
        await using (var dbA = _factory.CreateTestDbContext(tenantA))
        {
            dbA.Products.Add(new Product { Name = "Alpha Unique Widget", Sku = "SKU-ALPHA", Unit = "pcs" });
            await dbA.SaveChangesAsync();
        }

        // 2. Search customer/product in Tenant B
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/customers/search?q=Alpha");
        request.Headers.Add("X-Tenant-ID", tenantB);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify Tenant B has no records from Tenant A
        await using (var dbB = _factory.CreateTestDbContext(tenantB))
        {
            var productInB = dbB.Products.FirstOrDefault(p => p.Sku == "SKU-ALPHA");
            productInB.Should().BeNull();
        }
    }
}