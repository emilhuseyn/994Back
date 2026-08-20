using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECommerce.API.Controllers;

/// <summary>
/// 1C integration surface. Not part of the JWT-protected admin API — it
/// authenticates with the shared sync secret instead, so the 1C operator can
/// call it without an account.
///   • POST products — 1C pushes its catalogue (secret in the JSON `password`).
///   • GET  orders   — 1C pulls orders (secret via `?password=` or `X-Sync-Key`).
///   • GET  users    — 1C pulls customers (same auth as orders).
/// </summary>
[ApiController]
[Route("api/sync")]
[AllowAnonymous]
public class InventorySyncController : ControllerBase
{
    private readonly IInventorySyncService _service;
    private readonly SyncSettings _settings;

    public InventorySyncController(IInventorySyncService service, IOptions<SyncSettings> settings)
    {
        _service = service;
        _settings = settings.Value;
    }

    [HttpPost("products")]
    public async Task<ActionResult<ApiResponse<InventorySyncResultDto>>> SyncProducts(
        [FromBody] InventorySyncRequest request,
        CancellationToken ct)
    {
        if (!IsAuthorized(request.Password))
            return Unauthorized(ApiResponse<InventorySyncResultDto>.Fail(
                "Yanlış və ya çatışmayan sinxronizasiya açarı (password)."));

        var result = await _service.SyncAsync(request, ct);
        return Ok(ApiResponse<InventorySyncResultDto>.Ok(
            result,
            $"Sinxronizasiya tamamlandı: {result.ProductsCreated} yeni, {result.ProductsUpdated} yeniləndi."));
    }

    /// <summary>
    /// Pull orders. Auth via <c>?password=</c> query string or the
    /// <c>X-Sync-Key</c> header. Paged (default 100, max 500); <c>since</c>
    /// (ISO date) returns only orders created/updated on or after that moment.
    /// </summary>
    [HttpGet("orders")]
    public async Task<ActionResult<ApiResponse<SyncPagedResult<SyncOrderDto>>>> GetOrders(
        [FromQuery] string? password,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] DateTime? since = null,
        CancellationToken ct = default)
    {
        if (!IsAuthorized(string.IsNullOrEmpty(password) ? syncKey : password))
            return Unauthorized(ApiResponse<SyncPagedResult<SyncOrderDto>>.Fail(
                "Yanlış və ya çatışmayan sinxronizasiya açarı (password)."));

        var result = await _service.GetOrdersAsync(page, pageSize, since, ct);
        return Ok(ApiResponse<SyncPagedResult<SyncOrderDto>>.Ok(result));
    }

    /// <summary>Pull users/customers. Same auth + paging as <see cref="GetOrders"/>.</summary>
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<SyncPagedResult<SyncUserDto>>>> GetUsers(
        [FromQuery] string? password,
        [FromHeader(Name = "X-Sync-Key")] string? syncKey,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] DateTime? since = null,
        CancellationToken ct = default)
    {
        if (!IsAuthorized(string.IsNullOrEmpty(password) ? syncKey : password))
            return Unauthorized(ApiResponse<SyncPagedResult<SyncUserDto>>.Fail(
                "Yanlış və ya çatışmayan sinxronizasiya açarı (password)."));

        var result = await _service.GetUsersAsync(page, pageSize, since, ct);
        return Ok(ApiResponse<SyncPagedResult<SyncUserDto>>.Ok(result));
    }

    /// <summary>Constant-time comparison; fails closed when no secret is configured.</summary>
    private bool IsAuthorized(string? provided)
    {
        var expected = _settings.Password;
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
