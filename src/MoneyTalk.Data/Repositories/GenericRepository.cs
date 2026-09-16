using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Data.Repositories;

/// <summary>EF Core-backed repository. Entities returned by this repository stay attached to
/// the owning <see cref="MoneyTalkDbContext"/>'s change tracker: for aggregates with child
/// collections (Invoice+Lines, Bill+Lines, Budget+Lines, ...), the correct way to edit one is to
/// fetch it, mutate the same tracked instance's collection in place, then call
/// <c>IUnitOfWork.SaveChangesAsync</c> — <see cref="Update"/> is for entities with no child
/// collections, or for re-attaching a genuinely new, never-persisted aggregate root together
/// with its brand-new children (which is what <c>AddAsync</c> is for instead).</summary>
public class GenericRepository<T> : IRepository<T> where T : EntityBase
{
    private readonly MoneyTalkDbContext _context;
    private readonly Func<IQueryable<T>, IQueryable<T>>? _includeConfigurator;

    public GenericRepository(MoneyTalkDbContext context, Func<IQueryable<T>, IQueryable<T>>? includeConfigurator = null)
    {
        _context = context;
        _includeConfigurator = includeConfigurator;
    }

    private IQueryable<T> Query() => _includeConfigurator?.Invoke(_context.Set<T>()) ?? _context.Set<T>();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Query().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<T>> GetAllAsync(CancellationToken ct = default) =>
        Query().ToListAsync(ct);

    public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Query().Where(predicate).ToListAsync(ct);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Query().FirstOrDefaultAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await _context.Set<T>().AddAsync(entity, ct);

    public void Update(T entity) => _context.Set<T>().Update(entity);

    public void Remove(T entity) => _context.Set<T>().Remove(entity);
}
