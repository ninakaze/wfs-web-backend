using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Server_WFS.Application.Common;
using Server_WFS.Domain.Exceptions;

namespace Server_WFS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected string CurrentUserId
    {
        get
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedException("Không tìm thấy định danh người dùng trong Token.");
            }
            return userId;
        }
    }

    protected ActionResult<ApiResponse<T>> OkResponse<T>(T? data, string message = "Thao tác thành công.")
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    protected ActionResult<ApiResponse<string>> MessageResponse(string message)
    {
        return Ok(ApiResponse<string>.Ok(message, message));
    }
}
