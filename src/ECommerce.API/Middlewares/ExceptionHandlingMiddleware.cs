using System.Text.Json;
using ECommerce.Application.Common;
using FluentValidationException = FluentValidation.ValidationException;

namespace ECommerce.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (FluentValidationException ex)
        {
            await WriteAsync(context, 422, ApiResponse.Fail("Validasiya xətası.", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (Application.Common.ValidationException ex)
        {
            await WriteAsync(context, 422, ApiResponse.Fail(ex.Message, ex.Errors));
        }
        catch (AppException ex)
        {
            _logger.LogWarning(ex, "App exception: {Message}", ex.Message);
            await WriteAsync(context, ex.StatusCode, ApiResponse.Fail(ex.Message, ex.Errors));
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteAsync(context, 401, ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(context, 500, ApiResponse.Fail("Server xətası baş verdi."));
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int statusCode, ApiResponse body)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
