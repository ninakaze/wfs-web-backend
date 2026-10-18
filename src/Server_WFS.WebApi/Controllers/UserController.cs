using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server_WFS.Application.Common;
using Server_WFS.Application.DTOs.User;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Constants;

namespace Server_WFS.WebApi.Controllers;

/// <summary>
/// Quản lý thông tin tài khoản người dùng và thông tin cá nhân
/// </summary>
[Authorize]
public class UserController : BaseApiController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lấy thông tin hồ sơ của tài khoản đang đăng nhập (yêu cầu Bearer Token)
    /// </summary>
    /// <returns>Hồ sơ người dùng</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetMyProfile(CancellationToken ct)
    {
        var profile = await _userService.GetCurrentUserAsync(CurrentUserId, ct);
        return OkResponse(profile, "Lấy thông tin tài khoản thành công.");
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ cá nhân (Tên hiển thị, Ảnh đại diện, Số điện thoại)
    /// </summary>
    /// <param name="dto">Dữ liệu cập nhật</param>
    /// <param name="validator">Trình kiểm tra hợp lệ dữ liệu</param>
    /// <returns>Hồ sơ người dùng sau khi cập nhật</returns>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateMyProfile(
        [FromBody] UpdateProfileRequestDto dto,
        [FromServices] IValidator<UpdateProfileRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        var updatedProfile = await _userService.UpdateProfileAsync(CurrentUserId, dto, ct);
        return OkResponse(updatedProfile, "Cập nhật hồ sơ thành công!");
    }

    /// <summary>
    /// Đổi mật khẩu tài khoản
    /// </summary>
    /// <param name="dto">Mật khẩu hiện tại và Mật khẩu mới</param>
    /// <param name="validator">Trình kiểm tra hợp lệ dữ liệu</param>
    /// <returns>Thông báo trạng thái</returns>
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<string>>> ChangePassword(
        [FromBody] ChangePasswordRequestDto dto,
        [FromServices] IValidator<ChangePasswordRequestDto> validator,
        CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(dto, ct);
        await _userService.ChangePasswordAsync(CurrentUserId, dto, ct);
        return MessageResponse("Đổi mật khẩu thành công!");
    }

    /// <summary>
    /// Danh sách tất cả người dùng trong hệ thống (Dành riêng cho Quản trị viên - Role: Admin)
    /// </summary>
    /// <returns>Danh sách người dùng</returns>
    [HttpGet("all")]
    [Authorize(Roles = UserRoles.Admin)]
    [ProducesResponseType(typeof(ApiResponse<List<UserProfileDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<List<UserProfileDto>>>> GetAllUsers(CancellationToken ct)
    {
        var users = await _userService.GetAllUsersAsync(ct);
        return OkResponse(users, "Lấy danh sách người dùng thành công.");
    }
}
