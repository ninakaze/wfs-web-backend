using System.Text.Json;
using FirebaseAdmin.Auth;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Exceptions;
using Server_WFS.Infrastructure.Firebase;

using Microsoft.Extensions.Options;
using Server_WFS.Infrastructure.Configurations;

namespace Server_WFS.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly FirebaseProvider _firebaseProvider;
    private readonly FirebaseSettings _firebaseSettings;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        FirebaseProvider firebaseProvider, 
        IOptions<FirebaseSettings> firebaseOptions,
        ILogger<GoogleAuthService> logger)
    {
        _firebaseProvider = firebaseProvider;
        _firebaseSettings = firebaseOptions.Value;
        _logger = logger;
    }

    public async Task<ExternalAuthUserInfo> VerifyGoogleTokenAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new BadRequestException("Google IdToken không được để trống.");
        }

        // Cách 1: Thử verify qua Firebase Admin SDK (nếu đã kết nối live Firebase)
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirebaseApp != null)
        {
            try
            {
                var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, ct);
                var email = decodedToken.Claims.TryGetValue("email", out var emailObj) ? emailObj?.ToString() : "";
                var name = decodedToken.Claims.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : "";
                var picture = decodedToken.Claims.TryGetValue("picture", out var picObj) ? picObj?.ToString() : "";

                if (string.IsNullOrEmpty(email))
                {
                    throw new BadRequestException("Tài khoản Google này không có địa chỉ email công khai.");
                }

                return new ExternalAuthUserInfo
                {
                    Id = decodedToken.Uid,
                    Email = email,
                    Name = !string.IsNullOrWhiteSpace(name) ? name : email.Split('@')[0],
                    PictureUrl = picture
                };
            }
            catch (Exception ex) when (ex is not BadRequestException)
            {
                _logger.LogDebug("Verify bằng Firebase Admin SDK không thành công: {Msg}", ex.Message);
            }
        }

        // Cách 2: Xác thực chữ ký số bằng GoogleJsonWebSignature (cho Google One Tap / OAuth 2.0 ID Token)
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrWhiteSpace(_firebaseSettings.ClientId))
            {
                settings.Audience = new[] { _firebaseSettings.ClientId };
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            if (payload == null || string.IsNullOrEmpty(payload.Email))
            {
                throw new UnauthorizedException("Google ID Token không hợp lệ hoặc thiếu thông tin email.");
            }

            return new ExternalAuthUserInfo
            {
                Id = payload.Subject,
                Email = payload.Email,
                Name = !string.IsNullOrWhiteSpace(payload.Name) ? payload.Name : payload.Email.Split('@')[0],
                PictureUrl = payload.Picture
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xác thực Google ID Token");
            throw new UnauthorizedException("Mã xác thực Google ID Token không hợp lệ hoặc đã hết hạn.");
        }
    }
}

public class FacebookAuthService : IFacebookAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookAuthService> _logger;

    public FacebookAuthService(HttpClient httpClient, ILogger<FacebookAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ExternalAuthUserInfo> VerifyFacebookTokenAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new BadRequestException("Facebook Access Token không được để trống.");
        }

        try
        {
            var url = $"https://graph.facebook.com/me?fields=id,name,email,picture.width(400).height(400)&access_token={accessToken}";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Facebook Graph API trả về lỗi: {Error}", errorContent);
                throw new UnauthorizedException("Mã Facebook Access Token không hợp lệ hoặc đã hết hạn.");
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var id = root.GetProperty("id").GetString() ?? "";
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
            var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

            string? pictureUrl = null;
            if (root.TryGetProperty("picture", out var picProp) &&
                picProp.TryGetProperty("data", out var dataProp) &&
                dataProp.TryGetProperty("url", out var urlProp))
            {
                pictureUrl = urlProp.GetString();
            }

            // Nếu user đăng ký bằng số điện thoại trên FB và không có email
            if (string.IsNullOrWhiteSpace(email))
            {
                email = $"{id}@facebook.user";
            }

            return new ExternalAuthUserInfo
            {
                Id = id,
                Email = email,
                Name = !string.IsNullOrWhiteSpace(name) ? name : "Facebook User",
                PictureUrl = pictureUrl
            };
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "Lỗi khi gọi Facebook Graph API");
            throw new UnauthorizedException("Không thể kết nối đến dịch vụ xác thực Facebook.");
        }
    }
}
