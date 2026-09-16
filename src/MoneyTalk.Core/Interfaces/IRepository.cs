using System.Linq.Expressions;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.Core.Interfaces;

/// <summary>Minimal CRUD abstraction over a single aggregate type. Kept intentionally thin
/// (no paging/sorting ceremony) — reporting/aggregation queries that need more than this live
/// in dedicated service methods against <see cref="IUnitOfWork"/>, not bolted onto this
/// interface.</summary>
public interface IRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
