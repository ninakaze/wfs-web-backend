# WFS Enterprise Authentication & Cloud Backend API (.NET 9 + Firebase)

Dự án Backend C# (.NET 9) được thiết kế theo chuẩn **Clean Architecture** chuẩn doanh nghiệp lớn (Enterprise Grade), tích hợp với **Firebase (Authentication & Cloud Firestore)**, giao diện **Swagger UI** tương tác trực quan và hỗ trợ bảo mật JWT đa tầng.

---

## 🏛️ 1. Kiến trúc hệ thống (Clean Architecture)

Hệ thống được tổ chức thành 4 tầng riêng biệt, tuân thủ nguyên lý Single Responsibility và Dependency Inversion:

```
Server_WFS/
├── src/
│   ├── Server_WFS.Domain/            # Chứa Entities, Enums, Constants, Domain Exceptions
│   │   ├── Common/                   # BaseEntity (Id, CreatedAt, UpdatedAt)
│   │   ├── Constants/                # AuthProviders, UserRoles, OtpTypes
│   │   ├── Entities/                 # ApplicationUser, EmailOtp, RefreshToken
│   │   └── Exceptions/               # DomainExceptions (BadRequest, NotFound, Conflict,...)
│   │
│   ├── Server_WFS.Application/       # Chứa DTOs, Business Interfaces, FluentValidation
│   │   ├── Common/                   # ApiResponse<T> chuẩn mực RESTful
│   │   ├── DTOs/                     # AuthDtos, UserDtos
│   │   ├── Interfaces/               # IAuthService, IUserService, IFirestoreRepository, IEmailService,...
│   │   └── Validators/               # FluentValidation cho từng request
│   │
│   ├── Server_WFS.Infrastructure/    # Chứa Firebase/Firestore, Email, JWT, Bcrypt, OAuth2
│   │   ├── Configurations/           # FirebaseSettings, JwtSettings, EmailSettings
│   │   ├── Firebase/                 # FirebaseProvider (quản lý kết nối FirebaseApp & FirestoreDb)
│   │   ├── Persistence/              # FirestoreRepository (CRUD Firestore + Fallback an toàn)
│   │   └── Services/                 # AuthService, UserService, OtpService, EmailService, JwtTokenGenerator,...
│   │
│   └── Server_WFS.WebApi/            # Tầng Presentation API, Swagger UI, Middleware, Controllers
│       ├── Controllers/              # AuthController, UserController
│       ├── Extensions/               # SwaggerServiceExtensions (JWT Bearer Authorize button)
│       ├── Middlewares/              # ExceptionHandlingMiddleware (Bắt lỗi toàn cục)
│       ├── appsettings.json          # File cấu hình tham số hệ thống
│       └── Program.cs                # Khởi tạo DI và pipeline ASP.NET Core
└── Server_WFS.sln
```

---

## 🌟 2. Các tính năng nổi bật ("Chuẩn Dân Chuyên Nghiệp")

### ✉️ Đăng ký & Xác thực OTP qua Email:
- **Bước 1 (`POST /api/auth/register`)**: Người dùng nhập Email, Họ tên, Mật khẩu. Hệ thống kiểm tra trùng lặp, mã hóa mật khẩu bằng thuật toán **BCrypt Enhanced (Cost 12)**, tạo phiên đăng ký tạm và sinh mã **OTP 6 chữ số ngẫu nhiên cryptographically**.
- **Bước 2 (`POST /api/auth/verify-registration-otp`)**: Người dùng nhập mã OTP. Hệ thống xác minh mã, kích hoạt tài khoản chính thức, lưu vào **Firebase Cloud Firestore**, đồng bộ sang **Firebase Auth**, và cấp cặp **Access Token (JWT)** + **Refresh Token**.
- **Chống Spam & Brute Force**:
  - Cơ chế **Cooldown 60 giây** giữa các lần yêu cầu gửi lại mã OTP (`POST /api/auth/resend-otp`).
  - Hủy mã OTP tự động nếu nhập sai quá **5 lần** hoặc quá thời gian hết hạn **5 phút**.

### 🌐 Đăng nhập Mạng xã hội lớn & Số điện thoại (Google, Facebook, Phone SMS):
- **Đăng nhập Google (`POST /api/auth/google-login`)**:
  - Hỗ trợ cả **Firebase ID Token** (từ Firebase Client SDK trên Web/Mobile) lẫn **Google OAuth ID Token** (từ Google One Tap / Google Identity).
  - Tự động tạo hồ sơ người dùng mới nếu chưa tồn tại hoặc liên kết tài khoản.
- **Đăng nhập Facebook (`POST /api/auth/facebook-login`)**:
  - Xác thực trực tiếp qua Facebook Graph API để lấy thông tin `id`, `name`, `email`, `picture`.
- **Đăng nhập Số điện thoại (`POST /api/auth/phone-login`)**:
  - Xác thực mã SMS OTP qua Firebase Phone Authentication (reCAPTCHA verifier).
  - Backend đồng bộ ID Token, tự động tạo/cập nhật tài khoản người dùng theo số điện thoại và cấp phát JWT Token.

### 🔐 Bảo mật JWT & Refresh Token Rotation:
- Cấp phát Access Token chuẩn RFC 7519 chứa các claims định danh (`sub`, `email`, `name`, `role`, `jti`).
- Cơ chế **Token Rotation**: Khi gọi API `POST /api/auth/refresh-token`, Refresh Token cũ sẽ bị đánh dấu vô hiệu hóa ngay lập tức và cấp cặp Token mới, ngăn ngừa triệt để Replay Attack.
- API Đăng xuất (`POST /api/auth/revoke-token`) để hủy phiên làm việc.

### 🔑 Quên mật khẩu & Đặt lại mật khẩu an toàn:
- `POST /api/auth/forgot-password`: Gửi mã OTP xác nhận về email.
- `POST /api/auth/reset-password`: Xác thực OTP và cập nhật mật khẩu mới.

---

## 🚀 3. Hướng dẫn chạy khi Clone về máy (.env & dotnet)

### Bước 1: Thiết lập cấu hình biến môi trường (.env)
Dự án đã tách toàn bộ thông tin nhạy cảm vào biến môi trường, tuyệt đối **không hard-code**:
1. Copy file [.env.example](file:///.env.example) và đổi tên thành `.env`:
   ```bash
   cp .env.example .env
   # Hoặc trên Windows PowerShell:
   Copy-Item .env.example .env
   ```
2. Mở file `.env` và điền cấu hình của bạn/team (Firebase ProjectId, Secret Key, Gmail SMTP,...).

### Bước 2: Khởi chạy Server
```bash
# Khôi phục thư viện và build
dotnet restore
dotnet build

# Chạy server
dotnet run --project src/Server_WFS.WebApi/Server_WFS.WebApi.csproj --launch-profile http
```

### Mở Swagger UI:
Truy cập trình duyệt tại địa chỉ:
👉 **[http://localhost:5134](http://localhost:5134)** (hoặc cổng hiển thị trên Terminal).

### Hướng dẫn Test nhanh trên Swagger:
1. Mở nhóm **Auth**, chọn `POST /api/auth/register`, nhấn **Try it out**, nhập email và mật khẩu rồi bấm **Execute**.
2. Nhìn vào cửa sổ Terminal đang chạy, bạn sẽ thấy khung thông báo OTP nổi bật:
   ```
   ╔══════════════════════════════════════════════════════════════════════╗
   ║                    [DEV MOCK EMAIL OTP NOTIFICATION]                ║
   ╠══════════════════════════════════════════════════════════════════════╣
     Người nhận : Nguyen Van A <your_email@gmail.com>
     MÃ OTP     : >>> 123456 <<<
   ╚══════════════════════════════════════════════════════════════════════╝
   ```
3. Copy mã OTP gồm 6 chữ số này.
4. Mở API `POST /api/auth/verify-registration-otp`, nhập email và mã OTP vừa lấy rồi bấm **Execute**.
5. Nhận kết quả thành công chứa `accessToken`. Copy chuỗi `accessToken` này.
6. Bấm nút **Authorize** màu xanh lá ở góc trên bên phải màn hình Swagger. Nhập:
   ```
   Bearer <dán_access_token_vào_đây>
   ```
   Bấm **Authorize** -> **Close**.
7. Bây giờ bạn đã được xác thực! Hãy thử gọi các API trong mục **User** như `GET /api/user/me` hoặc `PUT /api/user/me`!

---

## 🔥 4. Hướng dẫn kết nối Firebase thật (https://console.firebase.google.com/)

Để lưu trữ dữ liệu vĩnh viễn trên đám mây của Google Firebase:

### Bước 1: Tạo Project trên Firebase Console
1. Truy cập [https://console.firebase.google.com/](https://console.firebase.google.com/) và đăng nhập tài khoản Google.
2. Bấm **Add project** (Thêm dự án), đặt tên dự án (ví dụ: `my-wfs-project`) và làm theo hướng dẫn tạo dự án.

### Bước 2: Kích hoạt Firestore Database
1. Trong thanh menu bên trái, chọn **Build** -> **Firestore Database**.
2. Bấm **Create database**.
3. Chọn khu vực (Location) gần bạn (ví dụ: `asia-southeast1` - Singapore) và chọn chế độ bắt đầu (Start in **test mode** hoặc **production mode**).

### Bước 3: Kích hoạt Firebase Authentication
1. Chọn **Build** -> **Authentication** -> Bấm **Get started**.
2. Tại tab **Sign-in method**, bấm kích hoạt các nhà cung cấp mong muốn:
   - **Email/Password**: Bật Enable.
   - **Google**: Bật Enable, chọn email hỗ trợ của bạn.
   - **Facebook**: Bật Enable (nếu có App ID và App Secret từ trang Facebook Developers).

### Bước 4: Tải file Private Key (Service Account JSON)
1. Bấm vào biểu tượng bánh răng **Project Settings** (Cài đặt dự án) ở góc trên bên trái.
2. Chọn tab **Service accounts**.
3. Chọn ngôn ngữ **.NET / General** và bấm nút **Generate new private key** (Tạo khóa riêng tư mới).
4. Một file `.json` sẽ được tải về máy của bạn.
5. Đổi tên file này thành:
   ```
   firebase-service-account.json
   ```
6. Copy file này vào thư mục:
   ```
   src/Server_WFS.WebApi/
   ```

### Bước 5: Cập nhật ProjectId trong `appsettings.json`
Mở file `src/Server_WFS.WebApi/appsettings.json` và điền `ProjectId` của dự án Firebase:
```json
"Firebase": {
  "ProjectId": "tên-project-id-firebase-của-bạn",
  "CredentialsPath": "firebase-service-account.json"
}
```
*Ngay khi bạn khởi động lại ứng dụng, hệ thống sẽ tự động phát hiện file chứng thực và lưu toàn bộ người dùng, mã OTP lên Google Cloud Firestore trong nháy mắt!*

---

## 📧 5. Hướng dẫn cấu hình gửi Email thật (Gmail App Password)

Khi bạn muốn email OTP gửi thẳng vào hộp thư Gmail thật của người dùng:

1. Vào tài khoản Google của bạn -> [Bảo mật tài khoản](https://myaccount.google.com/security).
2. Đảm bảo đã bật **Xác minh 2 bước**.
3. Tìm mục **Mật khẩu ứng dụng (App Passwords)** -> Đặt tên (ví dụ: `WFS Backend`) -> Tạo mã. Google sẽ cấp cho bạn một chuỗi 16 chữ cái (ví dụ: `abcd efgh ijkl mnop`).
4. Mở file `appsettings.json`, cập nhật:
   ```json
   "Email": {
     "SmtpServer": "smtp.gmail.com",
     "SmtpPort": 587,
     "SenderEmail": "email_cua_ban@gmail.com",
     "SenderName": "WFS Security Service",
     "Username": "email_cua_ban@gmail.com",
     "Password": "abcd efgh ijkl mnop",
     "EnableSsl": true,
     "UseDevelopmentMock": false
   }
   ```
5. Đặt `"UseDevelopmentMock": false` để kích hoạt gửi email qua mạng Internet thật.

---

## 🧪 6. Mã phản hồi chuẩn (Standard API Response)

Mọi API đều trả về cấu trúc thống nhất:
```json
{
  "success": true,
  "message": "Thao tác thành công.",
  "data": { ... },
  "errors": null,
  "timestamp": "2026-10-18T12:00:00Z"
}
```
Khi xảy ra lỗi (Validation, 400, 401, 403, 409, 500):
```json
{
  "success": false,
  "message": "Mã OTP không chính xác. Bạn còn 4 lần thử.",
  "data": null,
  "errors": [ "Mã OTP không chính xác. Bạn còn 4 lần thử." ],
  "timestamp": "2026-10-18T12:00:00Z"
}
```
