using System.Reflection;
using Microsoft.OpenApi.Models;

namespace Server_WFS.WebApi.Extensions;

public static class SwaggerServiceExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WFS Enterprise Authentication & Cloud Backend API",
                Version = "v1",
                Description = @"
### 🚀 Hệ Thống Backend .NET 9 Chuẩn Doanh Nghiệp (Enterprise Grade)
Tích hợp **Firebase (Authentication & Cloud Firestore)**, lưu trữ dữ liệu đám mây thời gian thực, bảo mật đa tầng.

#### 🌟 Các tính năng nổi bật:
- ✉️ **Đăng ký / Đăng nhập Email & Mật khẩu**: Quy trình gửi mã OTP 6 số qua email xác thực 2 bước chuyên nghiệp.
- 🌐 **Đăng nhập Mạng Xã Hội**: Hỗ trợ xác thực với **Google ID Token** và **Facebook Access Token**.
- 🔐 **Bảo mật JWT Bearer & Refresh Token**: Cấp phát Token chuẩn RFC 7519, cơ chế Token Rotation chống replay attack.
- 🛡️ **Clean Architecture**: Tách bạch 4 tầng Domain - Application - Infrastructure - WebApi, dễ dàng mở rộng và bảo trì.
- 📦 **Firebase Cloud Firestore**: Cơ sở dữ liệu NoSQL NoSQL cloud linh hoạt, tốc độ cao.

#### 🔑 Hướng dẫn sử dụng Token trên Swagger:
1. Gọi API `POST /api/auth/login` hoặc `POST /api/auth/verify-registration-otp`.
2. Sao chép chuỗi `accessToken` trong kết quả trả về.
3. Nhấp vào nút **Authorize** màu xanh lá ở góc trên bên phải trang Swagger này.
4. Nhập vào ô `Value`: `Bearer <token_của_bạn>` rồi nhấn **Authorize**.
",
                Contact = new OpenApiContact
                {
                    Name = "Đội ngũ phát triển WFS",
                    Email = "support@wfs.local"
                }
            });

            // Cấu hình nút Authorize Bearer JWT trong Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Nhập Token vào ô bên dưới theo định dạng: **Bearer {token}** (Ví dụ: `Bearer eyJhbGci...`)",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Đọc file XML Documentation để hiển thị ghi chú tiếng Việt trong Swagger
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "WFS Backend API v1");
            c.RoutePrefix = "swagger"; // Mở Swagger tại URL: http://localhost:PORT/swagger
            c.DocumentTitle = "WFS API Swagger UI - Chuẩn Dân Chuyên Nghiệp";
            c.DefaultModelsExpandDepth(-1); // Thu gọn model schema để UI sạch sẽ
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        });

        return app;
    }
}
