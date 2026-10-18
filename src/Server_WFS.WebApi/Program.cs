using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Server_WFS.Application;
using Server_WFS.Infrastructure;
using Server_WFS.WebApi.Extensions;
using Server_WFS.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// 0. Tự động nạp file .env vào cấu hình hệ thống
EnvLoader.Load(builder.Configuration);

// 1. Đăng ký các tầng kiến trúc Clean Architecture
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Đăng ký Controllers & API Explorer
builder.Services.AddControllers();

// 3. Cấu hình CORS bảo vệ tài nguyên chống tấn công Cross-Origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0 && !builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Trong môi trường Development: cho phép client local thử nghiệm
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// 4. Rate Limiting (.NET 9) theo từng địa chỉ IP của Client (chống spam OTP, Brute-Force và DoS)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// 5. Cấu hình Swagger UI chuyên nghiệp với Bearer Token
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// 6. Middleware xử lý ngoại lệ tập trung (Global Exception Handling)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 7. Kích hoạt Swagger UI (Chỉ bật trên môi trường Development để bảo mật Production)
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();

// 7. Xác thực & Phân quyền
app.UseAuthentication();
app.UseAuthorization();

// 8. Định tuyến Controllers & Web Client
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/client/index.html"));

app.Run();
