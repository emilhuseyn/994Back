using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    public AdminDashboardController(IDashboardService service) => _service = service;

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> Stats(CancellationToken ct)
        => Ok(ApiResponse<DashboardStatsDto>.Ok(await _service.GetStatsAsync(ct)));
}

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IUserAdminService _service;
    public AdminUsersController(IUserAdminService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedList<UserDto>>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? role = null,
        CancellationToken ct = default)
        => Ok(ApiResponse<PaginatedList<UserDto>>.Ok(await _service.ListAsync(page, pageSize, search, role, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Get(int id, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _service.GetByIdAsync(id, ct)));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _service.UpdateAsync(id, request, ct), "İstifadəçi yeniləndi."));

    [HttpPost("admins")]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateAdmin([FromBody] CreateAdminRequest request, CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _service.CreateAdminAsync(request, ct), "Admin yaradıldı."));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse>> Deactivate(int id, CancellationToken ct)
    {
        await _service.DeactivateAsync(id, ct);
        return Ok(ApiResponse.Ok("İstifadəçi deaktiv edildi."));
    }
}
