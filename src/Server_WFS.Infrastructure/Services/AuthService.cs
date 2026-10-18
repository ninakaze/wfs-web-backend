using System.Security.Claims;
using System.Text.Json;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Logging;
using Server_WFS.Application.DTOs.Auth;
using Server_WFS.Application.DTOs.User;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Constants;
using Server_WFS.Domain.Entities;
using Server_WFS.Domain.Exceptions;
using Server_WFS.Infrastructure.Firebase;

namespace Server_WFS.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IFirestoreRepository<ApplicationUser> _userRepository;
    private readonly IFirestoreRepository<EmailOtp> _otpRepository;
    private readonly IFirestoreRepository<RefreshToken> _refreshTokenRepository;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IFacebookAuthService _facebookAuthService;
    private readonly FirebaseProvider _firebaseProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IFirestoreRepository<ApplicationUser> userRepository,
        IFirestoreRepository<EmailOtp> otpRepository,
        IFirestoreRepository<RefreshToken> refreshTokenRepository,
        IOtpService otpService,
        IEmailService emailService,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher passwordHasher,
        IGoogleAuthService googleAuthService,
        IFacebookAuthService facebookAuthService,
        FirebaseProvider firebaseProvider,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _otpRepository = otpRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _otpService = otpService;
        _emailService = emailService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _googleAuthService = googleAuthService;
        _facebookAuthService = facebookAuthService;
        _firebaseProvider = firebaseProvider;
        _logger = logger;
    }

    private class PendingRegistrationData
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public async Task<string> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant().Trim();

        // 1. Kiểm tra email đã có tài khoản hay chưa
        var existingUser = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);
        if (existingUser != null)
        {
            throw new ConflictException("Email này đã được đăng ký trong hệ thống. Vui lòng đăng nhập hoặc sử dụng email khác.");
        }

        // 2. Hash mật khẩu và chuẩn bị dữ liệu tạm thời
        var passwordHash = _passwordHasher.HashPassword(dto.Password);
        var pendingData = new PendingRegistrationData
        {
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = dto.FullName.Trim()
        };
        var pendingJson = JsonSerializer.Serialize(pendingData);

        // 3. Sinh mã OTP và lưu vào database
        var otpCode = await _otpService.GenerateAndSaveOtpAsync(normalizedEmail, OtpTypes.Registration, pendingJson, ct);

        // 4. Gửi email chứa mã OTP
        await _emailService.SendOtpEmailAsync(normalizedEmail, dto.FullName, otpCode, OtpTypes.Registration, ct);

        return $"Mã xác thực OTP đã được gửi đến email {normalizedEmail}. Vui lòng nhập mã để hoàn tất đăng ký.";
    }

    public async Task<AuthResponseDto> VerifyOtpAndRegisterAsync(VerifyOtpRequestDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant().Trim();

        // 1. Xác thực OTP
        var otp = await _otpService.ValidateOtpAsync(normalizedEmail, dto.OtpCode, OtpTypes.Registration, ct);

        if (string.IsNullOrEmpty(otp.PendingData))
        {
            throw new BadRequestException("Không tìm thấy thông tin đăng ký chờ xác thực. Vui lòng đăng ký lại.");
        }

        var pendingData = JsonSerializer.Deserialize<PendingRegistrationData>(otp.PendingData);
        if (pendingData == null)
        {
            throw new BadRequestException("Dữ liệu đăng ký không hợp lệ. Vui lòng thử lại.");
        }

        // 2. Kiểm tra lại lần nữa phòng trường hợp race condition
        var existingUser = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);
        if (existingUser != null)
        {
            throw new ConflictException("Tài khoản đã tồn tại. Vui lòng đăng nhập.");
        }

        // 3. Tạo User entity
        var newUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = normalizedEmail,
            DisplayName = pendingData.FullName,
            PasswordHash = pendingData.PasswordHash,
            Role = UserRoles.User,
            Provider = AuthProviders.Local,
            IsEmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        // 4. Đồng bộ tạo tài khoản trên Firebase Authentication (nếu đã kết nối live Firebase)
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirebaseApp != null)
        {
            try
            {
                var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(new UserRecordArgs
                {
                    Uid = newUser.Id,
                    Email = newUser.Email,
                    DisplayName = newUser.DisplayName,
                    EmailVerified = true
                }, ct);
                _logger.LogInformation("Đã đồng bộ user sang Firebase Auth thành công: {Uid}", userRecord.Uid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đồng bộ user sang Firebase Auth: {Msg}", ex.Message);
            }
        }

        // 5. Lưu vào Firestore
        await _userRepository.AddAsync(newUser, ct);

        // 6. Đánh dấu OTP đã được sử dụng
        otp.IsUsed = true;
        await _otpRepository.UpdateAsync(otp, ct);

        // 7. Gửi email chào mừng thành viên mới
        _ = _emailService.SendWelcomeEmailAsync(newUser.Email, newUser.DisplayName, ct);

        // 8. Cấp JWT và Refresh Token
        var (accessToken, refreshToken, expiresIn) = await _jwtTokenGenerator.GenerateTokensAsync(newUser, ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToProfileDto(newUser)
        };
    }

    public async Task<string> ResendOtpAsync(ResendOtpRequestDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant().Trim();

        string? pendingData = null;

        if (dto.OtpType == OtpTypes.Registration)
        {
            var existingUser = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);
            if (existingUser != null)
            {
                throw new ConflictException("Email này đã được đăng ký và kích hoạt tài khoản trong hệ thống. Vui lòng đăng nhập.");
            }

            var existingOtps = await _otpRepository.QueryAsync(q => q
                .WhereEqualTo("email", normalizedEmail)
                .OrderByDescending("createdAt")
                .Limit(5), ct);
            var latestPending = existingOtps
                .Where(o => o.OtpType == OtpTypes.Registration && !o.IsUsed && !string.IsNullOrEmpty(o.PendingData))
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            if (latestPending == null)
            {
                throw new BadRequestException("Không tìm thấy thông tin đăng ký đang chờ xác thực cho email này hoặc mã OTP đã được sử dụng. Vui lòng đăng ký mới.");
            }

            pendingData = latestPending.PendingData;
        }
        else if (dto.OtpType == OtpTypes.ForgotPassword)
        {
            var user = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);
            if (user == null)
            {
                // Trả về thông báo giả để bảo mật không lộ email
                return $"Nếu email {normalizedEmail} tồn tại trong hệ thống, mã OTP mới đã được gửi.";
            }
        }

        var otpCode = await _otpService.GenerateAndSaveOtpAsync(normalizedEmail, dto.OtpType, pendingData, ct);
        await _emailService.SendOtpEmailAsync(normalizedEmail, "Bạn", otpCode, dto.OtpType, ct);

        return $"Mã OTP mới đã được gửi lại đến email {normalizedEmail}. Vui lòng kiểm tra hộp thư.";
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant().Trim();

        var user = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new UnauthorizedException("Email hoặc mật khẩu không chính xác.");
        }

        if (!_passwordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Email hoặc mật khẩu không chính xác.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);

        var (accessToken, refreshToken, expiresIn) = await _jwtTokenGenerator.GenerateTokensAsync(user, ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToProfileDto(user)
        };
    }

    public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthRequestDto dto, CancellationToken ct = default)
    {
        var googleUserInfo = await _googleAuthService.VerifyGoogleTokenAsync(dto.IdToken, ct);
        var normalizedEmail = googleUserInfo.Email.ToLowerInvariant().Trim();

        var user = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);

        if (user == null)
        {
            // Tạo tài khoản mới từ Google
            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = normalizedEmail,
                DisplayName = googleUserInfo.Name,
                PhotoUrl = googleUserInfo.PictureUrl,
                Role = UserRoles.User,
                Provider = AuthProviders.Google,
                ProviderId = googleUserInfo.Id,
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, ct);
            _logger.LogInformation("Tạo tài khoản mới từ Google Login thành công: {Email}", normalizedEmail);
        }
        else
        {
            if (!user.IsActive)
            {
                throw new ForbiddenException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");
            }

            // Cập nhật thông tin avatar hoặc ProviderId nếu chưa có
            if (string.IsNullOrEmpty(user.PhotoUrl) && !string.IsNullOrEmpty(googleUserInfo.PictureUrl))
            {
                user.PhotoUrl = googleUserInfo.PictureUrl;
            }
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, ct);
        }

        var (accessToken, refreshToken, expiresIn) = await _jwtTokenGenerator.GenerateTokensAsync(user, ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToProfileDto(user)
        };
    }

    public async Task<AuthResponseDto> FacebookLoginAsync(FacebookAuthRequestDto dto, CancellationToken ct = default)
    {
        var fbUserInfo = await _facebookAuthService.VerifyFacebookTokenAsync(dto.AccessToken, ct);
        var normalizedEmail = fbUserInfo.Email.ToLowerInvariant().Trim();

        var user = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);

        if (user == null)
        {
            // Tìm kiếm theo providerId nếu email là email tự tạo
            user = await _userRepository.GetByPropertyAsync("providerId", fbUserInfo.Id, ct);
        }

        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = normalizedEmail,
                DisplayName = fbUserInfo.Name,
                PhotoUrl = fbUserInfo.PictureUrl,
                Role = UserRoles.User,
                Provider = AuthProviders.Facebook,
                ProviderId = fbUserInfo.Id,
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, ct);
            _logger.LogInformation("Tạo tài khoản mới từ Facebook Login thành công: {Email}", normalizedEmail);
        }
        else
        {
            if (!user.IsActive)
            {
                throw new ForbiddenException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.");
            }

            if (string.IsNullOrEmpty(user.PhotoUrl) && !string.IsNullOrEmpty(fbUserInfo.PictureUrl))
            {
                user.PhotoUrl = fbUserInfo.PictureUrl;
            }
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, ct);
        }

        var (accessToken, refreshToken, expiresIn) = await _jwtTokenGenerator.GenerateTokensAsync(user, ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToProfileDto(user)
        };
    }

    public async Task<AuthResponseDto> PhoneLoginAsync(PhoneAuthRequestDto dto, CancellationToken ct = default)
    {
        string phone = "";
        string uid = "";

        if (!_firebaseProvider.IsLiveFirebaseReady || _firebaseProvider.FirebaseApp == null)
        {
            throw new ServiceUnavailableException("Dịch vụ xác thực số điện thoại Firebase chưa được cấu hình trên hệ thống. Vui lòng liên hệ quản trị viên.");
        }

        try
        {
            var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(dto.IdToken, ct);
            uid = decodedToken.Uid;
            if (decodedToken.Claims.TryGetValue("phone_number", out var pObj) && pObj != null)
            {
                phone = pObj.ToString()!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xác thực Firebase Phone ID Token");
            throw new UnauthorizedException("Mã xác thực số điện thoại (Phone Token) không hợp lệ hoặc đã hết hạn.");
        }

        if (string.IsNullOrEmpty(phone))
        {
            throw new BadRequestException("Không tìm thấy thông tin số điện thoại từ mã xác thực.");
        }

        var user = await _userRepository.GetByPropertyAsync("phoneNumber", phone, ct);

        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = !string.IsNullOrEmpty(uid) ? uid : Guid.NewGuid().ToString("N"),
                Email = $"{phone.Replace("+", "")}@phone.user",
                DisplayName = $"User {phone}",
                PhoneNumber = phone,
                Role = UserRoles.User,
                Provider = AuthProviders.Phone,
                ProviderId = uid,
                IsEmailVerified = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, ct);
            _logger.LogInformation("Tạo tài khoản mới từ Phone Auth thành công: {Phone}", phone);
        }
        else
        {
            if (!user.IsActive)
            {
                throw new ForbiddenException("Tài khoản của bạn đã bị khóa.");
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, ct);
        }

        var (accessToken, refreshToken, expiresIn) = await _jwtTokenGenerator.GenerateTokensAsync(user, ct);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToProfileDto(user)
        };
    }

    public async Task<string> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant().Trim();
        var user = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);

        if (user == null)
        {
            // Trả về thông báo thành công chung để bảo mật không lộ email có trong hệ thống hay không
            return $"Nếu email {normalizedEmail} tồn tại trong hệ thống, mã xác thực OTP đã được gửi đến hộp thư của bạn.";
        }

        var otpCode = await _otpService.GenerateAndSaveOtpAsync(normalizedEmail, OtpTypes.ForgotPassword, null, ct);
        await _emailService.SendOtpEmailAsync(normalizedEmail, user.DisplayName, otpCode, OtpTypes.ForgotPassword, ct);

        return $"Mã xác thực OTP đặt lại mật khẩu đã được gửi đến {normalizedEmail}. Vui lòng kiểm tra hộp thư.";
    }

    public async Task<string> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken ct = default)
    {
        var normalizedEmail = dto.Email.ToLowerInvariant().Trim();

        // 1. Xác thực OTP loại ForgotPassword
        var otp = await _otpService.ValidateOtpAsync(normalizedEmail, dto.OtpCode, OtpTypes.ForgotPassword, ct);

        // 2. Tìm user
        var user = await _userRepository.GetByPropertyAsync("email", normalizedEmail, ct);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy thông tin tài khoản.");
        }

        // 3. Đổi mật khẩu
        user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, ct);

        // 4. Đánh dấu OTP đã sử dụng
        otp.IsUsed = true;
        await _otpRepository.UpdateAsync(otp, ct);

        return "Mật khẩu của bạn đã được cập nhật thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken ct = default)
    {
        // 1. Lấy principal từ access token đã hết hạn
        var principal = _jwtTokenGenerator.GetPrincipalFromExpiredToken(dto.AccessToken);
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedException("Token không hợp lệ.");
        }

        // 2. Tìm Refresh Token trong database
        var storedToken = await _refreshTokenRepository.GetByPropertyAsync("token", dto.RefreshToken, ct);

        if (storedToken == null)
        {
            throw new UnauthorizedException("Refresh Token không tồn tại.");
        }

        if (storedToken.UserId != userId)
        {
            throw new UnauthorizedException("Refresh Token không khớp với tài khoản.");
        }

        if (storedToken.IsRevoked)
        {
            throw new UnauthorizedException("Refresh Token đã bị vô hiệu hóa.");
        }

        if (storedToken.IsUsed)
        {
            throw new UnauthorizedException("Refresh Token này đã từng được sử dụng.");
        }

        if (DateTime.UtcNow > storedToken.ExpiryDate)
        {
            throw new UnauthorizedException("Refresh Token đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // 3. Lấy thông tin user
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("Tài khoản không tồn tại hoặc đã bị khóa.");
        }

        // 4. Token Rotation: Đánh dấu RefreshToken cũ đã sử dụng
        storedToken.IsUsed = true;
        await _refreshTokenRepository.UpdateAsync(storedToken, ct);

        // 5. Cấp cặp Access Token & Refresh Token mới
        var (newAccessToken, newRefreshToken, expiresIn) = await _jwtTokenGenerator.GenerateTokensAsync(user, ct);

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            User = MapToProfileDto(user)
        };
    }

    public async Task RevokeTokenAsync(string refreshToken, string? currentUserId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new BadRequestException("RefreshToken không được để trống.");
        }

        var storedToken = await _refreshTokenRepository.GetByPropertyAsync("token", refreshToken, ct);
        if (storedToken == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(currentUserId) && storedToken.UserId != currentUserId)
        {
            throw new ForbiddenException("Bạn không có quyền thu hồi Refresh Token này.");
        }

        storedToken.IsRevoked = true;
        await _refreshTokenRepository.UpdateAsync(storedToken, ct);
    }

    private static UserProfileDto MapToProfileDto(ApplicationUser user)
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
