using Google.Cloud.Firestore;

namespace Server_WFS.Domain.Common;

public abstract class BaseEntity
{
    [FirestoreProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
