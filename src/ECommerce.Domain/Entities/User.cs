using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    // ── Email verification ──────────────────────────────────────────────
    /// <summary>True once the user has confirmed their email with a code.</summary>
    public bool IsEmailVerified { get; set; }
    /// <summary>Current 6-digit verification code (null once verified/expired).</summary>
    public string? EmailVerificationCode { get; set; }
    /// <summary>When the current code stops being valid.</summary>
    public DateTime? EmailVerificationCodeExpiresAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
