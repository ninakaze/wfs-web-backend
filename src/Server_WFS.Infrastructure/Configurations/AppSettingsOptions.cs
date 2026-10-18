namespace Server_WFS.Infrastructure.Configurations;

public class FirebaseSettings
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = "firebase-service-account.json";
    public string? CredentialsJson { get; set; }
    public bool UseEmulator { get; set; } = false;
    public string? EmulatorHost { get; set; }

    // Cấu hình Firebase Web/Mobile Client SDK (Public)
    public string ApiKey { get; set; } = string.Empty;
    public string AuthDomain { get; set; } = string.Empty;
    public string StorageBucket { get; set; } = string.Empty;
    public string MessagingSenderId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string MeasurementId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
}

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Server_WFS";
    public string Audience { get; set; } = "Server_WFS_Clients";
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}

public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = "noreply.wfs.server@gmail.com";
    public string SenderName { get; set; } = "WFS Authentication Service";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
    public bool UseDevelopmentMock { get; set; } = true;
}
