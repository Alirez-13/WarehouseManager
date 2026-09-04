namespace Warehouse.Api.Middleware;

using System.Net;
using System.Text.Json;
using Core.Exceptions;

public class ConcurrencyExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ConcurrencyExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ConcurrencyConflictException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict; // 409
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (InsufficientStockException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity; // 422
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }
}