using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Data.Repositories;

/// <summary>EF Core-backed repository. Entities returned by this repository stay attached to
/// the owning <see cref="MoneyTalkDbContext"/>'s change tracker: for aggregates with child
/// collections (Invoice+Lines, Bill+Lines, Budget+Lines, ...), the correct way to edit one is to
/// fetch it, mutate the same tracked instance's collection in place, then call
/// <c>IUnitOfWork.SaveChangesAsync</c> — calling <see cref="Update"/> afterward is harmless (it's
/// a no-op for anything already tracked, see its own doc comment) but not required.</summary>
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

    /// <summary>A no-op when <paramref name="entity"/> is already tracked by this context (the
    /// common case: it was fetched via <see cref="GetByIdAsync"/>/<see cref="FindAsync"/> earlier
    /// in the same unit of work and mutated in place) — automatic change detection during
    /// <c>SaveChanges</c> already picks up both scalar property changes and newly-added child
    /// entities in collection navigations correctly as Added. Calling
    /// <see cref="Microsoft.EntityFrameworkCore.DbSet{TEntity}.Update"/> in that case would
    /// re-walk the whole reachable object graph and reclassify those new children as Modified
    /// instead of Added — <see cref="EntityBase.Id"/> is a client-assigned Guid set the moment an
    /// entity is constructed, so a brand-new child looks "already existing" to that graph walk —
    /// producing a bogus "expected to affect 1 row(s), but actually affected 0" concurrency
    /// exception for a row that doesn't exist yet. Only a genuinely detached entity (e.g. loaded
    /// by a different, already-disposed context) needs the classic attach-and-mark-modified.</summary>
    public void Update(T entity)
    {
        if (_context.Entry(entity).State == EntityState.Detached)
            _context.Set<T>().Update(entity);
    }

    public void Remove(T entity) => _context.Set<T>().Remove(entity);
}
