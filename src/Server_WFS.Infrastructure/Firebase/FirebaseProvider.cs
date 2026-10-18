using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server_WFS.Infrastructure.Configurations;

namespace Server_WFS.Infrastructure.Firebase;

public class FirebaseProvider
{
    private readonly FirebaseSettings _settings;
    private readonly ILogger<FirebaseProvider> _logger;
    private FirestoreDb? _firestoreDb;
    private FirebaseApp? _firebaseApp;
    private bool _isLiveFirebaseReady = false;

    public FirebaseProvider(IOptions<FirebaseSettings> settings, ILogger<FirebaseProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        Initialize();
    }

    public FirestoreDb? FirestoreDb => _firestoreDb;
    public FirebaseApp? FirebaseApp => _firebaseApp;
    public bool IsLiveFirebaseReady => _isLiveFirebaseReady;
    public FirebaseSettings Settings => _settings;

    private void Initialize()
    {
        try
        {
            string credentialsPath = Path.IsPathRooted(_settings.CredentialsPath)
                ? _settings.CredentialsPath
                : Path.Combine(AppContext.BaseDirectory, _settings.CredentialsPath);

            // Also check current directory
            if (!File.Exists(credentialsPath))
            {
                credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), _settings.CredentialsPath);
            }

            GoogleCredential? credential = null;

#pragma warning disable CS0618
            if (File.Exists(credentialsPath))
            {
                _logger.LogInformation("Tìm thấy file chứng thực Firebase tại: {Path}", credentialsPath);
                credential = GoogleCredential.FromFile(credentialsPath);
            }
            else if (!string.IsNullOrWhiteSpace(_settings.CredentialsJson))
            {
                _logger.LogInformation("Sử dụng Firebase Credentials trực tiếp từ cấu hình JSON.");
                credential = GoogleCredential.FromJson(_settings.CredentialsJson);
            }
#pragma warning restore CS0618
            else
            {
                _logger.LogWarning("================================================================================");
                _logger.LogWarning("[FIREBASE THÔNG BÁO QUAN TRỌNG]");
                _logger.LogWarning("Chưa tìm thấy file cấu hình '{CredentialsPath}' từ https://console.firebase.google.com/.", _settings.CredentialsPath);
                _logger.LogWarning("Server sẽ tự động kích hoạt chế độ bộ nhớ đệm an toàn (In-Memory Repository)");
                _logger.LogWarning("giúp bạn test toàn bộ tính năng Đăng ký, OTP, Đăng nhập trên Swagger ngay lập tức!");
                _logger.LogWarning("Khi bạn tải file JSON về và đặt vào thư mục webapi, hệ thống sẽ tự động lưu lên Firestore.");
                _logger.LogWarning("================================================================================");
                return;
            }

            // Initialize FirebaseApp
            if (FirebaseApp.DefaultInstance == null)
            {
                _firebaseApp = FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = !string.IsNullOrEmpty(_settings.ProjectId) ? _settings.ProjectId : null
                });
                _logger.LogInformation("FirebaseApp đã được khởi tạo thành công: {Name}", _firebaseApp.Name);
            }
            else
            {
                _firebaseApp = FirebaseApp.DefaultInstance;
            }

            // Initialize Firestore
            string projectId = _settings.ProjectId;
            if (string.IsNullOrWhiteSpace(projectId) && credential != null)
            {
                // Try extract project ID from credential
                if (File.Exists(credentialsPath))
                {
                    var jsonContent = File.ReadAllText(credentialsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
                    if (doc.RootElement.TryGetProperty("project_id", out var projProp))
                    {
                        projectId = projProp.GetString() ?? "";
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(projectId))
            {
                var builder = new FirestoreDbBuilder
                {
                    ProjectId = projectId,
                    Credential = credential
                };
                _firestoreDb = builder.Build();
                _isLiveFirebaseReady = true;
                _logger.LogInformation("Kết nối Cloud Firestore thành công cho Project: {ProjectId}", projectId);
            }
            else
            {
                _logger.LogWarning("Không tìm thấy ProjectId cho Firestore. Cần cấu hình ProjectId trong appsettings.json.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong quá trình khởi tạo Firebase. Sẽ kích hoạt chế độ fallback an toàn.");
            _isLiveFirebaseReady = false;
        }
    }
}
