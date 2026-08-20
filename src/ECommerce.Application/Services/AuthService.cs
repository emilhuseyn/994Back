using AutoMapper;
using ECommerce.Application.Common;
using ECommerce.Application.DTOs;
using ECommerce.Application.Interfaces.Infrastructure;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Application.Services.Abstractions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly ICurrentUserService _current;
    private readonly IMapper _mapper;
    private readonly IEmailService _email;

    public AuthService(IUnitOfWork uow, IJwtTokenService jwt, IPasswordHasher hasher,
        ICurrentUserService current, IMapper mapper, IEmailService email)
    {
        _uow = uow;
        _jwt = jwt;
        _hasher = hasher;
        _current = current;
        _mapper = mapper;
        _email = email;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _uow.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Bu e-poçt artıq qeydiyyatdan keçib.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Customer,
            IsActive = true,
            IsEmailVerified = false,
        };
        IssueVerificationCode(user);
        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        SendVerificationEmail(user);

        // No token yet — the client must verify the email first.
        return new AuthResponse { RequiresVerification = true, Email = user.Email };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("E-poçt və ya şifrə yanlışdır.");

        // Required verification — block login until the email is confirmed.
        // We re-issue a fresh code and tell the client to go verify.
        if (!user.IsEmailVerified)
        {
            IssueVerificationCode(user);
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync(ct);
            SendVerificationEmail(user);
            return new AuthResponse { RequiresVerification = true, Email = user.Email };
        }

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var code = (request.Code ?? string.Empty).Trim();
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
            ?? throw new NotFoundException("İstifadəçi");

        // Already verified → just log them in.
        if (user.IsEmailVerified)
            return await BuildAuthResponseAsync(user, ct);

        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode)
            || user.EmailVerificationCodeExpiresAt is null
            || user.EmailVerificationCodeExpiresAt < DateTime.UtcNow)
            throw new ConflictException("Kodun vaxtı bitib. Zəhmət olmasa yenidən göndərin.");

        if (!string.Equals(user.EmailVerificationCode, code, StringComparison.Ordinal))
            throw new ConflictException("Kod yanlışdır.");

        user.IsEmailVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiresAt = null;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        // Verified → issue tokens (user is now logged in).
        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task ResendCodeAsync(ResendCodeRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        // Silently no-op for unknown / already-verified accounts (don't leak
        // whether an email is registered).
        if (user is null || user.IsEmailVerified) return;

        IssueVerificationCode(user);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
        SendVerificationEmail(user);
    }

    /// <summary>Generate a fresh 6-digit code valid for 10 minutes.</summary>
    private static void IssueVerificationCode(User user)
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var num = BitConverter.ToUInt32(bytes) % 1_000_000u;
        user.EmailVerificationCode = num.ToString("D6");
        user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.AddMinutes(10);
    }

    /// <summary>Fire-and-forget the verification-code email (never blocks).</summary>
    private void SendVerificationEmail(User user)
    {
        if (string.IsNullOrWhiteSpace(user.EmailVerificationCode)) return;
        var html = EmailTemplates.VerificationCode(user.FullName, user.EmailVerificationCode);
        _ = _email.SendAsync(
            user.Email,
            user.FullName,
            $"Təsdiq kodu · {user.EmailVerificationCode}",
            html);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedException("Refresh token boş ola bilməz.");

        var user = await _uow.Users.FirstOrDefaultAsync(u =>
            u.RefreshToken == refreshToken && u.RefreshTokenExpiresAt > DateTime.UtcNow, ct);
        if (user is null) throw new UnauthorizedException("Refresh token etibarsızdır.");

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<UserDto> GetMeAsync(CancellationToken ct = default)
    {
        if (_current.UserId is null) throw new UnauthorizedException();
        var user = await _uow.Users.GetByIdAsync(_current.UserId.Value, ct)
            ?? throw new NotFoundException("İstifadəçi");
        return _mapper.Map<UserDto>(user);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, CancellationToken ct)
    {
        var refresh = _jwt.GenerateRefreshToken();
        user.RefreshToken = refresh;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var access = _jwt.GenerateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            User = _mapper.Map<UserDto>(user)
        };
    }
}
