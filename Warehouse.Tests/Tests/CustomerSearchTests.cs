using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Core.Entities;
using Warehouse.Tests.Fixtures;
using Xunit;

namespace Warehouse.Tests.Tests;

public class CustomerSearchTests : IClassFixture<WarehouseTestFactory>
{
    private readonly WarehouseTestFactory _factory;
    private readonly HttpClient _client;

    public CustomerSearchTests(WarehouseTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CustomerSearch_Matches_Name_Infix_And_Phone_Prefix()
    {
        var tenantId = "tenant_search_" + Guid.NewGuid();

        await using (var context = _factory.CreateTestDbContext(tenantId))
        {
            context.Customers.AddRange(
                new Customer { Name = "Robert Johnson", Phone = "+14155551234" },
                new Customer { Name = "Roberta Williams", Phone = "+14155555678" },
                new Customer { Name = "Sarah Connor", Phone = "+12065559999" }
            );
            await context.SaveChangesAsync();
        }

        // Test 1: Match Name by partial text ("obert")
        var req1 = new HttpRequestMessage(HttpMethod.Get, "/api/customers/search?q=obert");
        req1.Headers.Add("X-Tenant-ID", tenantId);
        var res1 = await _client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        var matches1 = await res1.Content.ReadFromJsonAsync<List<CustomerDto>>();
        matches1.Should().HaveCount(2);
        matches1.Should().Contain(c => c.Name == "Robert Johnson");
        matches1.Should().Contain(c => c.Name == "Roberta Williams");

        // Test 2: Match Phone by prefix ("+1206")
        var req2 = new HttpRequestMessage(HttpMethod.Get, "/api/customers/search?q=%2B1206");
        req2.Headers.Add("X-Tenant-ID", tenantId);
        var res2 = await _client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        var matches2 = await res2.Content.ReadFromJsonAsync<List<CustomerDto>>();
        matches2.Should().HaveCount(1);
        matches2!.First().Name.Should().Be("Sarah Connor");

        // Test 3: Respect limit parameter
        var req3 = new HttpRequestMessage(HttpMethod.Get, "/api/customers/search?q=obert&limit=1");
        req3.Headers.Add("X-Tenant-ID", tenantId);
        var res3 = await _client.SendAsync(req3);
        var matches3 = await res3.Content.ReadFromJsonAsync<List<CustomerDto>>();
        matches3.Should().HaveCount(1);
    }

    private record CustomerDto(int Id, string Name, string Phone, string? Email, string? Address);
}