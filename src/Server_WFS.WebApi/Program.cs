using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Server_WFS.Application;
using Server_WFS.Infrastructure;
using Server_WFS.WebApi.Extensions;
using Server_WFS.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// 0. Tự động nạp cấu hình từ .env và biến môi trường
EnvLoader.Load(builder.Configuration);

// 0.1 Cấu hình ForwardedHeaders cho Reverse Proxy (Render, Fly.io, Cloudflare, Docker)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

    // Giới hạn nghiêm ngặt hơn cho các API gửi OTP qua Email (tối đa 5 yêu cầu/phút/IP) để chống DoS và cạn kiệt hạn ngạch SMTP
    options.AddPolicy("OtpRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// 5. Cấu hình Swagger UI chuyên nghiệp với Bearer Token
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// Áp dụng ForwardedHeaders ngay đầu pipeline để nhận diện đúng HTTPS và Client IP
app.UseForwardedHeaders();

// 6. Middleware xử lý ngoại lệ tập trung (Global Exception Handling)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 7. Kích hoạt Swagger UI (Bật trên Development hoặc khi có biến ENABLE_SWAGGER=true trên Render)
var enableSwagger = builder.Configuration.GetValue<bool>("ENABLE_SWAGGER", false) ||
                    builder.Configuration.GetValue<bool>("EnableSwagger", false);
if (app.Environment.IsDevelopment() || enableSwagger)
{
    app.UseSwaggerDocumentation();
}

var hasHttpsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTPS_PORT")) ||
    builder.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) == true ||
    !string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_HTTPS_PORT"]);

if (hasHttpsConfigured)
{
    app.UseHttpsRedirection();
}
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
