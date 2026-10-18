using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Server_WFS.Application.Common;
using Server_WFS.Application.DTOs.Auth;
using Server_WFS.Application.Interfaces;

namespace Server_WFS.WebApi.Controllers;

/// <summary>
/// Quản lý toàn bộ quy trình Xác thực: Đăng ký OTP, Đăng nhập, Google Login, Facebook Login, Đổi mật khẩu
/// </summary>
[EnableRateLimiting("AuthRateLimit")]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Bước 1: Đăng ký tài khoản mới bằng Email và Mật khẩu (Hệ thống sẽ gửi mã OTP qua email)
    /// </summary>
    /// <param name="dto">Thông tin đăng ký gồm Email, Họ tên, Mật khẩu</param>
    /// <param name="validator">Trình kiểm tra hợp lệ dữ liệu đầu vào</param>
    /// <returns>Thông báo xác nhận mã OTP đã được gửi đến email</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<string>>> Register(
        [FromBody] RegisterRequestDto dto,
        [FromServices] IValidator<RegisterRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var resultMessage = await _authService.RegisterAsync(dto, ct);
        return MessageResponse(resultMessage);
    }

    /// <summary>
    /// Bước 2: Xác thực mã OTP 6 số để kích hoạt tài khoản và nhận Access Token
    /// </summary>
    /// <param name="dto">Email, Mã OTP 6 chữ số</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Access Token, Refresh Token và thông tin người dùng</returns>
    [HttpPost("verify-registration-otp")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> VerifyRegistrationOtp(
        [FromBody] VerifyOtpRequestDto dto,
        [FromServices] IValidator<VerifyOtpRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var authResponse = await _authService.VerifyOtpAndRegisterAsync(dto, ct);
        return OkResponse(authResponse, "Xác thực OTP thành công! Tài khoản của bạn đã được kích hoạt.");
    }

    /// <summary>
    /// Gửi lại mã OTP qua email (có cơ chế Cooldown chống spam 60s)
    /// </summary>
    /// <param name="dto">Email cần nhận lại mã</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Thông báo trạng thái gửi mã</returns>
    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<string>>> ResendOtp(
        [FromBody] ResendOtpRequestDto dto,
        [FromServices] IValidator<ResendOtpRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var message = await _authService.ResendOtpAsync(dto, ct);
        return MessageResponse(message);
    }

    /// <summary>
    /// Đăng nhập bằng Email và Mật khẩu
    /// </summary>
    /// <param name="dto">Email và Mật khẩu</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Access Token (JWT) và Refresh Token</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequestDto dto,
        [FromServices] IValidator<LoginRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var authResponse = await _authService.LoginAsync(dto, ct);
        return OkResponse(authResponse, "Đăng nhập thành công!");
    }

    /// <summary>
    /// Đăng nhập / Đăng ký nhanh bằng Google (sử dụng Google ID Token hoặc Firebase ID Token)
    /// </summary>
    /// <param name="dto">Google IdToken thu được từ Google Sign-In hoặc Firebase Client SDK</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Access Token (JWT) và thông tin tài khoản</returns>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> GoogleLogin(
        [FromBody] GoogleAuthRequestDto dto,
        [FromServices] IValidator<GoogleAuthRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var authResponse = await _authService.GoogleLoginAsync(dto, ct);
        return OkResponse(authResponse, "Đăng nhập bằng tài khoản Google thành công!");
    }

    /// <summary>
    /// Đăng nhập / Đăng ký nhanh bằng Facebook (sử dụng Facebook User Access Token)
    /// </summary>
    /// <param name="dto">Facebook Access Token từ Facebook SDK</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Access Token (JWT) và thông tin tài khoản</returns>
    [HttpPost("facebook-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> FacebookLogin(
        [FromBody] FacebookAuthRequestDto dto,
        [FromServices] IValidator<FacebookAuthRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var authResponse = await _authService.FacebookLoginAsync(dto, ct);
        return OkResponse(authResponse, "Đăng nhập bằng tài khoản Facebook thành công!");
    }

    /// <summary>
    /// Đăng nhập / Đăng ký bằng Số điện thoại (Firebase Phone Authentication ID Token)
    /// </summary>
    /// <param name="dto">Firebase ID Token nhận được sau khi xác minh mã SMS qua Firebase SDK</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Access Token (JWT) và thông tin tài khoản</returns>
    [HttpPost("phone-login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> PhoneLogin(
        [FromBody] PhoneAuthRequestDto dto,
        [FromServices] IValidator<PhoneAuthRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var authResponse = await _authService.PhoneLoginAsync(dto, ct);
        return OkResponse(authResponse, "Đăng nhập bằng số điện thoại thành công!");
    }

    /// <summary>
    /// Quên mật khẩu: Gửi mã OTP xác thực qua email để đặt lại mật khẩu
    /// </summary>
    /// <param name="dto">Email tài khoản</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Thông báo xác nhận</returns>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<string>>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto dto,
        [FromServices] IValidator<ForgotPasswordRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var message = await _authService.ForgotPasswordAsync(dto, ct);
        return MessageResponse(message);
    }

    /// <summary>
    /// Đặt lại mật khẩu mới bằng mã OTP đã nhận
    /// </summary>
    /// <param name="dto">Email, Mã OTP và Mật khẩu mới</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Thông báo đổi mật khẩu thành công</returns>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<string>>> ResetPassword(
        [FromBody] ResetPasswordRequestDto dto,
        [FromServices] IValidator<ResetPasswordRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var message = await _authService.ResetPasswordAsync(dto, ct);
        return MessageResponse(message);
    }

    /// <summary>
    /// Làm mới Access Token khi hết hạn bằng Refresh Token (Token Rotation)
    /// </summary>
    /// <param name="dto">Cặp Access Token cũ và Refresh Token</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Cặp Access Token và Refresh Token mới</returns>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken(
        [FromBody] RefreshTokenRequestDto dto,
        [FromServices] IValidator<RefreshTokenRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var authResponse = await _authService.RefreshTokenAsync(dto, ct);
        return OkResponse(authResponse, "Làm mới Token thành công!");
    }

    /// <summary>
    /// Đăng xuất tài khoản (Vô hiệu hóa Refresh Token an toàn qua Request Body)
    /// </summary>
    /// <param name="dto">Refresh Token cần hủy</param>
    /// <param name="validator">Trình kiểm tra dữ liệu</param>
    /// <returns>Thông báo đăng xuất thành công</returns>
    [Authorize]
    [HttpPost("revoke-token")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<string>>> RevokeToken(
        [FromBody] RevokeTokenRequestDto dto,
        [FromServices] IValidator<RevokeTokenRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await _authService.RevokeTokenAsync(dto.RefreshToken, currentUserId, ct);
        return MessageResponse("Đăng xuất thành công, phiên đăng nhập đã được hủy.");
    }
}
