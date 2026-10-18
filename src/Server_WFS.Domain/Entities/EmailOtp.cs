using Google.Cloud.Firestore;
using Server_WFS.Domain.Common;
using Server_WFS.Domain.Constants;

namespace Server_WFS.Domain.Entities;

[FirestoreData]
public class EmailOtp : BaseEntity
{
    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("otpCode")]
    public string OtpCode { get; set; } = string.Empty;

    [FirestoreProperty("otpType")]
    public string OtpType { get; set; } = OtpTypes.Registration;

    [FirestoreProperty("expiryTime")]
    public DateTime ExpiryTime { get; set; }

    [FirestoreProperty("isUsed")]
    public bool IsUsed { get; set; } = false;

    [FirestoreProperty("attemptCount")]
    public int AttemptCount { get; set; } = 0;

    [FirestoreProperty("pendingData")]
    public string? PendingData { get; set; }
}
