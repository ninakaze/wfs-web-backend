using System.Linq.Expressions;
using Google.Cloud.Firestore;
using Server_WFS.Domain.Common;

namespace Server_WFS.Application.Interfaces;

public interface IFirestoreRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<T?> GetByPropertyAsync(string propertyName, object value, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task<List<T>> GetListByPropertyAsync(string propertyName, object value, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task<T> UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<List<T>> QueryAsync(Func<Query, Query> queryBuilder, CancellationToken ct = default);
}
