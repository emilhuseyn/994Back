using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/theme")]
public class ThemeController : ControllerBase
{
    private readonly IThemeService _service;
    public ThemeController(IThemeService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ThemeDto>>> Get(CancellationToken ct)
        => Ok(ApiResponse<ThemeDto>.Ok(await _service.GetAsync(ct)));
}

[ApiController]
[Route("api/admin/theme")]
[Authorize(Roles = "Admin")]
public class AdminThemeController : ControllerBase
{
    private readonly IThemeService _service;
    public AdminThemeController(IThemeService service) => _service = service;

    [HttpPut]
    public async Task<ActionResult<ApiResponse<ThemeDto>>> Update(
        [FromBody] ThemeDto theme, CancellationToken ct)
        => Ok(ApiResponse<ThemeDto>.Ok(await _service.UpdateAsync(theme, ct), "Mövzu yeniləndi."));

    [HttpPost("reset")]
    public async Task<ActionResult<ApiResponse<ThemeDto>>> Reset(CancellationToken ct)
        => Ok(ApiResponse<ThemeDto>.Ok(await _service.ResetAsync(ct), "Mövzu defaulta sıfırlandı."));
}
