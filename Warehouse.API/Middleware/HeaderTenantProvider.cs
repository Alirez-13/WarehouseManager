namespace Warehouse.Api.Middleware;

using Core.Interfaces;

public class HeaderTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeaderTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext
                          ?? throw new InvalidOperationException("No active HTTP context present.");

        if (httpContext.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantHeader) &&
            !string.IsNullOrWhiteSpace(tenantHeader))
        {
            return tenantHeader.ToString();
        }

        throw new BadHttpRequestException("Missing required 'X-Tenant-ID' request header.");
    }
}