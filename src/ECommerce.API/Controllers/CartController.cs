using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/cart")]
[AllowAnonymous]
public class CartController : ControllerBase
{
    private readonly ICartService _service;
    public CartController(ICartService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<CartDto>>> Get(CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _service.GetAsync(ct)));

    [HttpPost("items")]
    public async Task<ActionResult<ApiResponse<CartDto>>> AddItem([FromBody] AddCartItemRequest request, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _service.AddItemAsync(request, ct), "Səbətə əlavə edildi."));

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<ApiResponse<CartDto>>> Update(int id, [FromBody] UpdateCartItemRequest request, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _service.UpdateItemAsync(id, request, ct), "Səbət yeniləndi."));

    [HttpDelete("items/{id:int}")]
    public async Task<ActionResult<ApiResponse<CartDto>>> Remove(int id, CancellationToken ct)
        => Ok(ApiResponse<CartDto>.Ok(await _service.RemoveItemAsync(id, ct), "Element silindi."));

    [HttpDelete("clear")]
    public async Task<ActionResult<ApiResponse>> Clear(CancellationToken ct)
    {
        await _service.ClearAsync(ct);
        return Ok(ApiResponse.Ok("Səbət təmizləndi."));
    }
}
