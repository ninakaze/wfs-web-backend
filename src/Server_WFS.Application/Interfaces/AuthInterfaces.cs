using System.Security.Claims;
using Server_WFS.Application.DTOs.Auth;
using Server_WFS.Application.DTOs.User;
using Server_WFS.Domain.Entities;

namespace Server_WFS.Application.Interfaces;

public interface IAuthService
{
    Task<string> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> VerifyOtpAndRegisterAsync(VerifyOtpRequestDto dto, CancellationToken ct = default);
    Task<string> ResendOtpAsync(ResendOtpRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> FacebookLoginAsync(FacebookAuthRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> PhoneLoginAsync(PhoneAuthRequestDto dto, CancellationToken ct = default);
    Task<string> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken ct = default);
    Task<string> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, string? currentUserId = null, CancellationToken ct = default);
}

public interface IUserService
{
    Task<UserProfileDto> GetCurrentUserAsync(string userId, CancellationToken ct = default);
    Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequestDto dto, CancellationToken ct = default);
    Task ChangePasswordAsync(string userId, ChangePasswordRequestDto dto, CancellationToken ct = default);
    Task<List<UserProfileDto>> GetAllUsersAsync(CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string toName, string otpCode, string otpType, CancellationToken ct = default);
    Task SendWelcomeEmailAsync(string toEmail, string toName, CancellationToken ct = default);
}

public interface IOtpService
{
    Task<string> GenerateAndSaveOtpAsync(string email, string otpType, string? pendingData = null, CancellationToken ct = default);
    Task<EmailOtp> ValidateOtpAsync(string email, string otpCode, string otpType, CancellationToken ct = default);
}

public interface IJwtTokenGenerator
{
    Task<(string AccessToken, string RefreshToken, int ExpiresIn)> GenerateTokensAsync(ApplicationUser user, CancellationToken ct = default);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

public class ExternalAuthUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
}

public interface IGoogleAuthService
{
    Task<ExternalAuthUserInfo> VerifyGoogleTokenAsync(string idToken, CancellationToken ct = default);
}

public interface IFacebookAuthService
{
    Task<ExternalAuthUserInfo> VerifyFacebookTokenAsync(string accessToken, CancellationToken ct = default);
}
