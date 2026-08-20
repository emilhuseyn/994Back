using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;
    public OrdersController(IOrderService service) => _service = service;

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _service.CreateAsync(request, ct), "Sifariş yaradıldı."));

    [HttpGet("my")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> Mine(CancellationToken ct)
        => Ok(ApiResponse<List<OrderDto>>.Ok(await _service.ListMineAsync(ct)));
}

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _service;
    public AdminOrdersController(IOrderService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedList<OrderDto>>>> List(
        [FromQuery] OrderQueryParameters query, CancellationToken ct = default)
        => Ok(ApiResponse<PaginatedList<OrderDto>>.Ok(await _service.ListAllAsync(query, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Get(int id, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateStatus(int id, [FromBody] UpdateOrderStatusRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrderDto>.Ok(await _service.UpdateStatusAsync(id, request, ct), "Status yeniləndi."));
}
