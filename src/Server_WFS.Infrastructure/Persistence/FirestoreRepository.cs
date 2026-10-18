using System.Collections.Concurrent;
using System.Reflection;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Common;
using Server_WFS.Infrastructure.Firebase;

namespace Server_WFS.Infrastructure.Persistence;

public class FirestoreRepository<T> : IFirestoreRepository<T> where T : BaseEntity
{
    private readonly FirebaseProvider _firebaseProvider;
    private readonly ILogger<FirestoreRepository<T>> _logger;
    private readonly string _collectionName;

    // In-memory fallback cache (used when Firebase credentials are not yet configured)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, T>> _inMemoryStores = new();

    public FirestoreRepository(FirebaseProvider firebaseProvider, ILogger<FirestoreRepository<T>> logger)
    {
        _firebaseProvider = firebaseProvider;
        _logger = logger;
        _collectionName = GetCollectionName();
    }

    private static string GetCollectionName()
    {
        var typeName = typeof(T).Name;
        return typeName switch
        {
            "ApplicationUser" => "users",
            "EmailOtp" => "email_otps",
            "RefreshToken" => "refresh_tokens",
            _ => typeName.ToLowerInvariant() + "s"
        };
    }

    private ConcurrentDictionary<string, T> GetMemoryStore()
    {
        return _inMemoryStores.GetOrAdd(_collectionName, _ => new ConcurrentDictionary<string, T>());
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var docRef = _firebaseProvider.FirestoreDb.Collection(_collectionName).Document(id);
                var snapshot = await docRef.GetSnapshotAsync(ct);
                if (!snapshot.Exists) return null;

                var entity = snapshot.ConvertTo<T>();
                entity.Id = snapshot.Id;
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc document {Id} từ Firestore collection {Col}", id, _collectionName);
                throw;
            }
        }

        // In-memory fallback
        var store = GetMemoryStore();
        store.TryGetValue(id, out var item);
        return item;
    }

    public async Task<T?> GetByPropertyAsync(string propertyName, object value, CancellationToken ct = default)
    {
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var query = _firebaseProvider.FirestoreDb.Collection(_collectionName)
                    .WhereEqualTo(propertyName, value)
                    .Limit(1);

                var snapshot = await query.GetSnapshotAsync(ct);
                var doc = snapshot.Documents.FirstOrDefault();
                if (doc == null || !doc.Exists) return null;

                var entity = doc.ConvertTo<T>();
                entity.Id = doc.Id;
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi truy vấn thuộc tính {Prop}={Val} từ collection {Col}", propertyName, value, _collectionName);
                throw;
            }
        }

        // In-memory fallback
        var store = GetMemoryStore();
        return store.Values.FirstOrDefault(item =>
        {
            var prop = typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;
            var val = prop.GetValue(item);
            if (val == null && value == null) return true;
            return val != null && val.ToString()!.Equals(value?.ToString(), StringComparison.OrdinalIgnoreCase);
        });
    }

    public async Task<List<T>> GetListByPropertyAsync(string propertyName, object value, CancellationToken ct = default)
    {
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var query = _firebaseProvider.FirestoreDb.Collection(_collectionName)
                    .WhereEqualTo(propertyName, value);

                var snapshot = await query.GetSnapshotAsync(ct);
                var list = new List<T>();
                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        var entity = doc.ConvertTo<T>();
                        entity.Id = doc.Id;
                        list.Add(entity);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi truy vấn danh sách thuộc tính {Prop}={Val} từ {Col}", propertyName, value, _collectionName);
                throw;
            }
        }

        var store = GetMemoryStore();
        return store.Values.Where(item =>
        {
            var prop = typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return false;
            var val = prop.GetValue(item);
            if (val == null && value == null) return true;
            return val != null && val.ToString()!.Equals(value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var snapshot = await _firebaseProvider.FirestoreDb.Collection(_collectionName).GetSnapshotAsync(ct);
                var list = new List<T>();
                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        var entity = doc.ConvertTo<T>();
                        entity.Id = doc.Id;
                        list.Add(entity);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đọc toàn bộ dữ liệu từ collection {Col}", _collectionName);
                throw;
            }
        }

        var store = GetMemoryStore();
        return store.Values.ToList();
    }

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString("N");
        }
        entity.CreatedAt = DateTime.UtcNow;

        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var docRef = _firebaseProvider.FirestoreDb.Collection(_collectionName).Document(entity.Id);
                await docRef.SetAsync(entity, null, ct);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thêm mới document {Id} vào collection {Col}", entity.Id, _collectionName);
                throw;
            }
        }

        var store = GetMemoryStore();
        store[entity.Id] = entity;
        return entity;
    }

    public async Task<T> UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var docRef = _firebaseProvider.FirestoreDb.Collection(_collectionName).Document(entity.Id);
                await docRef.SetAsync(entity, SetOptions.Overwrite, ct);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật document {Id} trong collection {Col}", entity.Id, _collectionName);
                throw;
            }
        }

        var store = GetMemoryStore();
        store[entity.Id] = entity;
        return entity;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var docRef = _firebaseProvider.FirestoreDb.Collection(_collectionName).Document(id);
                await docRef.DeleteAsync(null, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xóa document {Id} trong collection {Col}", id, _collectionName);
                throw;
            }
        }

        var store = GetMemoryStore();
        store.TryRemove(id, out _);
    }

    public async Task<List<T>> QueryAsync(Func<Query, Query> queryBuilder, CancellationToken ct = default)
    {
        if (_firebaseProvider.IsLiveFirebaseReady && _firebaseProvider.FirestoreDb != null)
        {
            try
            {
                var baseQuery = (Query)_firebaseProvider.FirestoreDb.Collection(_collectionName);
                var finalQuery = queryBuilder(baseQuery);
                var snapshot = await finalQuery.GetSnapshotAsync(ct);

                var list = new List<T>();
                foreach (var doc in snapshot.Documents)
                {
                    if (doc.Exists)
                    {
                        var entity = doc.ConvertTo<T>();
                        entity.Id = doc.Id;
                        list.Add(entity);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi thực hiện custom query trên collection {Col}", _collectionName);
                throw;
            }
        }

        return await GetAllAsync(ct);
    }
}
