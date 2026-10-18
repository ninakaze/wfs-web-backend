using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server_WFS.Application.Common;
using Server_WFS.Infrastructure.Configurations;

namespace Server_WFS.WebApi.Controllers;

/// <summary>
/// Cung cấp cấu hình công khai phục vụ Web/Mobile Frontend Client
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly FirebaseSettings _firebaseSettings;

    public ConfigController(IOptions<FirebaseSettings> firebaseOptions)
    {
        _firebaseSettings = firebaseOptions.Value;
    }

    /// <summary>
    /// Lấy cấu hình Firebase công khai phục vụ Client SDK (Được nạp động từ file .env của hệ thống)
    /// </summary>
    /// <returns>Cấu hình Firebase Client SDK</returns>
    [HttpGet("firebase")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<object>> GetFirebaseClientConfig()
    {
        var config = new
        {
            apiKey = _firebaseSettings.ApiKey,
            authDomain = !string.IsNullOrEmpty(_firebaseSettings.AuthDomain)
                ? _firebaseSettings.AuthDomain
                : $"{_firebaseSettings.ProjectId}.firebaseapp.com",
            projectId = _firebaseSettings.ProjectId,
            storageBucket = !string.IsNullOrEmpty(_firebaseSettings.StorageBucket)
                ? _firebaseSettings.StorageBucket
                : $"{_firebaseSettings.ProjectId}.firebasestorage.app",
            messagingSenderId = _firebaseSettings.MessagingSenderId,
            appId = _firebaseSettings.AppId,
            measurementId = _firebaseSettings.MeasurementId
        };

        return Ok(ApiResponse<object>.Ok(config, "Lấy cấu hình Firebase Client thành công."));
    }
}
