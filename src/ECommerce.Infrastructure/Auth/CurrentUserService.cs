using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Infrastructure.Auth;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;
    public const string SessionHeader = "X-Session-Id";

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int? UserId
    {
        get
        {
            var sub = _accessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                   ?? _accessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => _accessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public UserRole? Role
    {
        get
        {
            var role = _accessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(role, true, out var r) ? r : (UserRole?)null;
        }
    }

    public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(UserRole role) => Role == role;

    public string? GetSessionId()
    {
        var ctx = _accessor.HttpContext;
        if (ctx is null) return null;
        if (ctx.Request.Headers.TryGetValue(SessionHeader, out var values) && !string.IsNullOrWhiteSpace(values.ToString()))
            return values.ToString();
        return ctx.Request.Cookies.TryGetValue(SessionHeader, out var cookie) ? cookie : null;
    }
}
