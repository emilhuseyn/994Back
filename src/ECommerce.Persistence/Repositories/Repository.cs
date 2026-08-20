using System.Linq.Expressions;
using ECommerce.Application.Interfaces.Persistence;
using ECommerce.Domain.Common;
using ECommerce.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly ApplicationDbContext _ctx;
    protected readonly DbSet<T> _set;

    public Repository(ApplicationDbContext ctx)
    {
        _ctx = ctx;
        _set = ctx.Set<T>();
    }

    public IQueryable<T> Query(bool tracking = false) =>
        tracking ? _set.AsQueryable() : _set.AsNoTracking();

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.FirstOrDefaultAsync(predicate, ct);

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        IQueryable<T> q = _set;
        if (predicate is not null) q = q.Where(predicate);
        return q.ToListAsync(ct);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate is null ? _set.CountAsync(ct) : _set.CountAsync(predicate, ct);

    public Task AddAsync(T entity, CancellationToken ct = default) =>
        _set.AddAsync(entity, ct).AsTask();

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        _set.AddRangeAsync(entities, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
