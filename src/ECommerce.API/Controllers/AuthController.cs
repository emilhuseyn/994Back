using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.RegisterAsync(request, ct), "Qeydiyyat tamamlandı."));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.LoginAsync(request, ct), "Giriş edildi."));

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.VerifyEmailAsync(request, ct), "E-poçt təsdiqləndi."));

    [HttpPost("resend-code")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> ResendCode([FromBody] ResendCodeRequest request, CancellationToken ct)
    {
        await _auth.ResendCodeAsync(request, ct);
        return Ok(ApiResponse.Ok("Kod yenidən göndərildi."));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => Ok(ApiResponse<AuthResponse>.Ok(await _auth.RefreshTokenAsync(request.RefreshToken, ct)));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct)
        => Ok(ApiResponse<UserDto>.Ok(await _auth.GetMeAsync(ct)));
}
