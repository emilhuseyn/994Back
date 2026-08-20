using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace ECommerce.Application.Interfaces.Infrastructure;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int? ValidateAndGetUserId(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(UserRole role);
    string? GetSessionId();
}

public interface IFileStorageService
{
    Task<string> SaveAsync(IFormFile file, string subfolder, CancellationToken ct = default);
    Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default);
}

/// <summary>
/// Thin wrapper around the Google Gemini REST API for the storefront's
/// AI-stylist feature.  Implementations are responsible for transport
/// concerns (retry, timeout, authentication); the prompt itself is owned
/// by the calling service so the same client can serve future LLM tasks.
/// </summary>
public interface IGeminiClient
{
    /// <summary>True when an API key is configured — callers should fall
    /// back to non-AI behaviour when this returns false.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Calls Gemini 2.0 Flash with the given prompt and asks for a JSON
    /// response (Gemini's <c>response_mime_type</c> mode).  Returns the
    /// raw JSON text on success, or <c>null</c> when the call fails or
    /// the key is missing.
    /// </summary>
    Task<string?> GenerateJsonAsync(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Transactional email sender (SMTP).  Implementations must NEVER throw —
/// a failed email must not break the user's order/contact flow.  Callers
/// typically fire-and-forget.
/// </summary>
public interface IEmailService
{
    /// <summary>True when SMTP credentials are configured.</summary>
    bool IsConfigured { get; }

    /// <summary>The store's own mailbox — where admin notifications are sent.</summary>
    string? AdminEmail { get; }

    /// <summary>
    /// Send one HTML email.  Returns true on success, false on any failure
    /// (logged internally).  Does not throw.
    /// </summary>
    Task<bool> SendAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}
