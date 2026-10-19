using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using Server_WFS.Application.Common;
using Server_WFS.Domain.Exceptions;

namespace Server_WFS.WebApi.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var apiResponse = new ApiResponse<object>();

        switch (exception)
        {
            case ValidationException validationEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                var errors = validationEx.Errors.Select(e => e.ErrorMessage).Distinct().ToList();
                var primaryError = errors.FirstOrDefault() ?? "Dữ liệu đầu vào không hợp lệ.";
                apiResponse = ApiResponse<object>.Fail(primaryError, errors);
                _logger.LogWarning("Dữ liệu validation không hợp lệ: {Errors}", string.Join(", ", errors));
                break;

            case AppException appEx:
                response.StatusCode = appEx.StatusCode;
                apiResponse = ApiResponse<object>.Fail(appEx.Message);
                _logger.LogWarning("Nghiệp vụ trả về lỗi ({StatusCode}): {Message}", appEx.StatusCode, appEx.Message);
                break;

            case SecurityTokenException tokenEx:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                apiResponse = ApiResponse<object>.Fail("Phiên xác thực không hợp lệ hoặc đã hết hạn: " + tokenEx.Message);
                _logger.LogWarning(tokenEx, "Lỗi token xác thực");
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                apiResponse = ApiResponse<object>.Fail("Đã xảy ra lỗi nội bộ máy chủ. Vui lòng thử lại sau.");
                _logger.LogError(exception, "Lỗi không xác định hệ thống: {Message}", exception.Message);
                break;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var result = JsonSerializer.Serialize(apiResponse, jsonOptions);
        await response.WriteAsync(result);
    }
}
