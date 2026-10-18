using Microsoft.Extensions.Configuration;

namespace Server_WFS.WebApi.Extensions;

/// <summary>
/// Trình tải biến môi trường tự động từ file .env cho toàn bộ hệ thống
/// </summary>
public static class EnvLoader
{
    /// <summary>
    /// Tìm kiếm và nạp các biến từ file .env vào Environment và ConfigurationManager
    /// </summary>
    public static void Load(ConfigurationManager configuration)
    {
        var envFilePath = FindEnvFilePath();

        if (string.IsNullOrEmpty(envFilePath) || !File.Exists(envFilePath))
        {
            Console.WriteLine("[EnvLoader] Không tìm thấy file .env, hệ thống sẽ sử dụng cấu hình từ appsettings hoặc biến môi trường hệ thống.");
            return;
        }

        Console.WriteLine($"[EnvLoader] Đang nạp cấu hình từ file: {envFilePath}");

        var envDictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(envFilePath))
        {
            var line = rawLine.Trim();

            // Bỏ qua dòng trống hoặc ghi chú (#)
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line.Substring(0, separatorIndex).Trim();
            var value = line.Substring(separatorIndex + 1).Trim();

            // Loại bỏ dấu ngoặc kép hoặc ngoặc đơn nếu có
            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
            {
                if (value.Length >= 2)
                {
                    value = value.Substring(1, value.Length - 2);
                }
            }

            // 1. Nạp vào Process Environment Variables
            Environment.SetEnvironmentVariable(key, value);

            // 2. Tự động ánh xạ các key thông dụng sang ASP.NET Configuration Key (Section:Property)
            MapConfigurationKey(key, value, envDictionary);
        }

        // Nạp vào builder.Configuration
        if (envDictionary.Count > 0)
        {
            configuration.AddInMemoryCollection(envDictionary);
            Console.WriteLine($"[EnvLoader] Đã nạp thành công {envDictionary.Count} biến cấu hình vào hệ thống.");
        }
    }

    private static void MapConfigurationKey(string key, string value, Dictionary<string, string?> dict)
    {
        // Giữ nguyên key gốc và key chuyển đổi gạch dưới kép __
        dict[key] = value;
        if (key.Contains("__"))
        {
            dict[key.Replace("__", ":")] = value;
        }

        // Tự động ánh xạ các tên biến môi trường thân thiện
        switch (key.ToUpperInvariant())
        {
            // Firebase
            case "FIREBASE_PROJECT_ID":
                dict["Firebase:ProjectId"] = value;
                break;
            case "FIREBASE_CREDENTIALS_PATH":
                dict["Firebase:CredentialsPath"] = value;
                break;
            case "FIREBASE_CREDENTIALS_JSON":
                dict["Firebase:CredentialsJson"] = value;
                break;
            case "FIREBASE_API_KEY":
                dict["Firebase:ApiKey"] = value;
                break;
            case "FIREBASE_AUTH_DOMAIN":
                dict["Firebase:AuthDomain"] = value;
                break;
            case "FIREBASE_STORAGE_BUCKET":
                dict["Firebase:StorageBucket"] = value;
                break;
            case "FIREBASE_MESSAGING_SENDER_ID":
                dict["Firebase:MessagingSenderId"] = value;
                break;
            case "FIREBASE_APP_ID":
                dict["Firebase:AppId"] = value;
                break;
            case "FIREBASE_MEASUREMENT_ID":
                dict["Firebase:MeasurementId"] = value;
                break;
            case "FIREBASE_CLIENT_ID":
            case "GOOGLE_CLIENT_ID":
                dict["Firebase:ClientId"] = value;
                break;

            // JWT
            case "JWT_SECRET_KEY":
                dict["Jwt:SecretKey"] = value;
                break;
            case "JWT_ISSUER":
                dict["Jwt:Issuer"] = value;
                break;
            case "JWT_AUDIENCE":
                dict["Jwt:Audience"] = value;
                break;
            case "JWT_ACCESS_TOKEN_EXPIRY_MINUTES":
                dict["Jwt:AccessTokenExpiryMinutes"] = value;
                break;
            case "JWT_REFRESH_TOKEN_EXPIRY_DAYS":
                dict["Jwt:RefreshTokenExpiryDays"] = value;
                break;

            // Email
            case "EMAIL_SMTP_SERVER":
                dict["Email:SmtpServer"] = value;
                break;
            case "EMAIL_SMTP_PORT":
                dict["Email:SmtpPort"] = value;
                break;
            case "EMAIL_SENDER_EMAIL":
                dict["Email:SenderEmail"] = value;
                break;
            case "EMAIL_SENDER_NAME":
                dict["Email:SenderName"] = value;
                break;
            case "EMAIL_USERNAME":
                dict["Email:Username"] = value;
                break;
            case "EMAIL_PASSWORD":
                dict["Email:Password"] = value;
                break;
            case "EMAIL_ENABLE_SSL":
                dict["Email:EnableSsl"] = value;
                break;
            case "EMAIL_USE_MOCK":
                dict["Email:UseDevelopmentMock"] = value;
                break;
        }
    }

    private static string? FindEnvFilePath()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        // Kiểm tra thư mục hiện tại và tối đa 4 cấp thư mục cha
        var dir = currentDir;
        for (int i = 0; i < 5 && dir != null; i++)
        {
            var potentialPath = Path.Combine(dir.FullName, ".env");
            if (File.Exists(potentialPath))
            {
                return potentialPath;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
