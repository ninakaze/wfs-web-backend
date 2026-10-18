using Microsoft.Extensions.Logging;
using Server_WFS.Application.DTOs.User;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Constants;
using Server_WFS.Domain.Entities;
using Server_WFS.Domain.Exceptions;

namespace Server_WFS.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IFirestoreRepository<ApplicationUser> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IFirestoreRepository<ApplicationUser> userRepository,
        IPasswordHasher passwordHasher,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            throw new NotFoundException("Không tìm thấy thông tin tài khoản người dùng.");
        }

        return MapToDto(user);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequestDto dto, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        user.DisplayName = dto.DisplayName.Trim();
        if (dto.PhoneNumber != null) user.PhoneNumber = dto.PhoneNumber;
        if (dto.PhotoUrl != null) user.PhotoUrl = dto.PhotoUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, ct);
        _logger.LogInformation("Người dùng {UserId} đã cập nhật hồ sơ cá nhân", userId);

        return MapToDto(user);
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequestDto dto, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            throw new NotFoundException("Không tìm thấy người dùng.");
        }

        if (user.Provider != AuthProviders.Local || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new BadRequestException("Tài khoản đăng nhập qua mạng xã hội (Google/Facebook) không thể đổi mật khẩu qua kênh này.");
        }

        if (!_passwordHasher.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
        {
            throw new BadRequestException("Mật khẩu hiện tại không chính xác.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);

        _logger.LogInformation("Người dùng {UserId} đã đổi mật khẩu thành công", userId);
    }

    public async Task<List<UserProfileDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);
        return users.Select(MapToDto).ToList();
    }

    private static UserProfileDto MapToDto(ApplicationUser user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            PhotoUrl = user.PhotoUrl,
            Role = user.Role,
            Provider = user.Provider,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
