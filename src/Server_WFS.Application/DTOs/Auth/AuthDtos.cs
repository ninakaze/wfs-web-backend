using Server_WFS.Domain.Constants;

namespace Server_WFS.Application.DTOs.Auth;

/// <summary>
/// Yêu cầu đăng ký tài khoản mới bằng Email và Mật khẩu (Bước 1: Gửi OTP xác thực)
/// </summary>
public class RegisterRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu xác thực mã OTP đăng ký để kích hoạt tài khoản (Bước 2)
/// </summary>
public class VerifyOtpRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public string OtpType { get; set; } = OtpTypes.Registration;
}

/// <summary>
/// Yêu cầu gửi lại mã OTP (chống spam với cooldown)
/// </summary>
public class ResendOtpRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string OtpType { get; set; } = OtpTypes.Registration;
}

/// <summary>
/// Yêu cầu đăng nhập tài khoản bằng Email và Mật khẩu
/// </summary>
public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Đăng nhập bằng Google ID Token (từ Firebase SDK hoặc Google Sign-In)
/// </summary>
public class GoogleAuthRequestDto
{
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>
/// Đăng nhập bằng Facebook Access Token (từ Facebook Login SDK)
/// </summary>
public class FacebookAuthRequestDto
{
    public string AccessToken { get; set; } = string.Empty;
}

/// <summary>
/// Đăng nhập bằng Phone Auth Token (Firebase Phone Authentication ID Token)
/// </summary>
public class PhoneAuthRequestDto
{
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu gửi OTP quên mật khẩu
/// </summary>
public class ForgotPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Xác nhận OTP và đặt lại mật khẩu mới
/// </summary>
public class ResetPasswordRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Làm mới access token khi hết hạn bằng refresh token
/// </summary>
public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Yêu cầu thu hồi Refresh Token (Đăng xuất an toàn qua Request Body)
/// </summary>
public class RevokeTokenRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả trả về sau khi đăng nhập / xác thực thành công
/// </summary>
public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public User.UserProfileDto User { get; set; } = null!;
}
