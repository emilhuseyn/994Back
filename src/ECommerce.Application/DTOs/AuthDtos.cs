using ECommerce.Domain.Enums;

namespace ECommerce.Application.DTOs;

public record RegisterRequest(
    string FullName,
    string Email,
    string? PhoneNumber,
    string Password);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record VerifyEmailRequest(string Email, string Code);

public record ResendCodeRequest(string Email);

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto? User { get; set; }

    /// <summary>
    /// True when the account exists but the email isn't verified yet.  In
    /// this case the tokens are empty and the client must send the user to
    /// the verification screen with <see cref="Email"/>.
    /// </summary>
    public bool RequiresVerification { get; set; }
    /// <summary>The email awaiting verification (set when RequiresVerification).</summary>
    public string? Email { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record UpdateUserRequest(
    string FullName,
    string? PhoneNumber,
    UserRole Role,
    bool IsActive);

public record CreateAdminRequest(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber);
