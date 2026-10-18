using Google.Cloud.Firestore;
using Server_WFS.Domain.Common;
using Server_WFS.Domain.Constants;

namespace Server_WFS.Domain.Entities;

[FirestoreData]
public class ApplicationUser : BaseEntity
{
    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("passwordHash")]
    public string? PasswordHash { get; set; }

    [FirestoreProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [FirestoreProperty("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [FirestoreProperty("photoUrl")]
    public string? PhotoUrl { get; set; }

    [FirestoreProperty("role")]
    public string Role { get; set; } = UserRoles.User;

    [FirestoreProperty("provider")]
    public string Provider { get; set; } = AuthProviders.Local;

    [FirestoreProperty("providerId")]
    public string? ProviderId { get; set; }

    [FirestoreProperty("isEmailVerified")]
    public bool IsEmailVerified { get; set; } = false;

    [FirestoreProperty("isActive")]
    public bool IsActive { get; set; } = true;

    [FirestoreProperty("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }
}
