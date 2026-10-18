using Google.Cloud.Firestore;
using Server_WFS.Domain.Common;

namespace Server_WFS.Domain.Entities;

[FirestoreData]
public class RefreshToken : BaseEntity
{
    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("token")]
    public string Token { get; set; } = string.Empty;

    [FirestoreProperty("jwtId")]
    public string JwtId { get; set; } = string.Empty;

    [FirestoreProperty("isUsed")]
    public bool IsUsed { get; set; } = false;

    [FirestoreProperty("isRevoked")]
    public bool IsRevoked { get; set; } = false;

    [FirestoreProperty("expiryDate")]
    public DateTime ExpiryDate { get; set; }
}
