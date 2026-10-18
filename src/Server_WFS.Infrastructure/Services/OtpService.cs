using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Entities;
using Server_WFS.Domain.Exceptions;

namespace Server_WFS.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IFirestoreRepository<EmailOtp> _otpRepository;
    private readonly ILogger<OtpService> _logger;
    private const int OtpValidityMinutes = 5;
    private const int ResendCooldownSeconds = 60;
    private const int MaxAttempts = 5;

    // Lock theo email để ngăn ngừa Race Condition khi gửi requests song song dò OTP
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _emailLocks = new();

    public OtpService(IFirestoreRepository<EmailOtp> otpRepository, ILogger<OtpService> logger)
    {
        _otpRepository = otpRepository;
        _logger = logger;
    }

    private static string HashOtpCode(string email, string code)
    {
        var raw = $"{email.ToLowerInvariant().Trim()}:{code.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static bool SlowEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    public async Task<string> GenerateAndSaveOtpAsync(
        string email, 
        string otpType, 
        string? pendingData = null, 
        CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();

        // 1. Kiểm tra cooldown chống spam gửi lại liên tục (Tối ưu chỉ đọc 5 bản ghi mới nhất)
        var existingOtps = await _otpRepository.QueryAsync(q => q
            .WhereEqualTo("email", normalizedEmail)
            .OrderByDescending("createdAt")
            .Limit(5), ct);
        var activeOtp = existingOtps
            .Where(o => o.OtpType == otpType && !o.IsUsed && o.ExpiryTime > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (activeOtp != null)
        {
            var elapsed = DateTime.UtcNow - activeOtp.CreatedAt;
            if (elapsed.TotalSeconds < ResendCooldownSeconds)
            {
                var remainingSeconds = (int)(ResendCooldownSeconds - elapsed.TotalSeconds);
                throw new BadRequestException($"Vui lòng đợi {remainingSeconds} giây trước khi yêu cầu mã OTP mới.");
            }

            // Hủy mã cũ trước khi sinh mã mới
            activeOtp.IsUsed = true;
            await _otpRepository.UpdateAsync(activeOtp, ct);
        }

        // 2. Sinh mã OTP 6 số ngẫu nhiên cryptographically
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hashedOtp = HashOtpCode(normalizedEmail, otpCode);

        // 3. Lưu vào Database (chỉ lưu Hash, tuyệt đối không lưu Plaintext)
        var otpEntity = new EmailOtp
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = normalizedEmail,
            OtpCode = hashedOtp,
            OtpType = otpType,
            ExpiryTime = DateTime.UtcNow.AddMinutes(OtpValidityMinutes),
            IsUsed = false,
            AttemptCount = 0,
            PendingData = pendingData,
            CreatedAt = DateTime.UtcNow
        };

        await _otpRepository.AddAsync(otpEntity, ct);
        _logger.LogInformation("Đã tạo OTP {Type} bảo mật thành công cho email {Email}", otpType, normalizedEmail);

        return otpCode;
    }

    public async Task<EmailOtp> ValidateOtpAsync(
        string email, 
        string otpCode, 
        string otpType, 
        CancellationToken ct = default)
    {
        var normalizedEmail = email.ToLowerInvariant().Trim();
        var emailLock = _emailLocks.GetOrAdd(normalizedEmail, _ => new SemaphoreSlim(1, 1));

        await emailLock.WaitAsync(ct);
        try
        {
            var existingOtps = await _otpRepository.QueryAsync(q => q
                .WhereEqualTo("email", normalizedEmail)
                .OrderByDescending("createdAt")
                .Limit(5), ct);
            var otp = existingOtps
                .Where(o => o.OtpType == otpType && !o.IsUsed)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            if (otp == null)
            {
                throw new BadRequestException("Không tìm thấy mã OTP hoặc mã OTP đã được sử dụng. Vui lòng yêu cầu mã mới.");
            }

            if (otp.AttemptCount >= MaxAttempts)
            {
                otp.IsUsed = true;
                await _otpRepository.UpdateAsync(otp, ct);
                throw new BadRequestException("Bạn đã nhập sai mã OTP quá 5 lần. Mã này đã bị hủy, vui lòng yêu cầu mã mới.");
            }

            if (DateTime.UtcNow > otp.ExpiryTime)
            {
                otp.IsUsed = true;
                await _otpRepository.UpdateAsync(otp, ct);
                throw new BadRequestException("Mã OTP đã hết hạn (chỉ có hiệu lực trong 5 phút). Vui lòng yêu cầu mã mới.");
            }

            var inputHashed = HashOtpCode(normalizedEmail, otpCode);
            bool isMatch = false;

            // Kiểm tra hash bảo mật hoặc mã cũ
            if (otp.OtpCode.Length == 64)
            {
                isMatch = SlowEquals(otp.OtpCode, inputHashed);
            }
            else
            {
                isMatch = SlowEquals(otp.OtpCode, otpCode.Trim());
            }

            if (!isMatch)
            {
                otp.AttemptCount++;
                await _otpRepository.UpdateAsync(otp, ct);
                int remaining = MaxAttempts - otp.AttemptCount;
                if (remaining <= 0)
                {
                    otp.IsUsed = true;
                    await _otpRepository.UpdateAsync(otp, ct);
                    throw new BadRequestException("Bạn đã nhập sai mã OTP quá 5 lần. Mã này đã bị hủy.");
                }
                throw new BadRequestException($"Mã OTP không chính xác. Bạn còn {remaining} lần thử.");
            }

            return otp;
        }
        finally
        {
            emailLock.Release();
        }
    }
}
