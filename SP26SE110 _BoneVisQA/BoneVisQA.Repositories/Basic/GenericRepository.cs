using BoneVisQA.Repositories.DBContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace BoneVisQA.Repositories.Basic
{
    public class GenericRepository<TEntity> where TEntity : class
    {
        protected readonly BoneVisQADbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public GenericRepository(BoneVisQADbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        // Get All (Async & Sync)
        public virtual async Task<List<TEntity>> GetAllAsync()
            => await _dbSet.ToListAsync();

        public virtual List<TEntity> GetAll()
            => _dbSet.ToList();

        // Find (Async & Sync)
        public virtual async Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> filter)
            => await _dbSet.Where(filter).ToListAsync();

        public virtual List<TEntity> Find(Expression<Func<TEntity, bool>> filter)
            => _dbSet.Where(filter).ToList();

        // GetById (Async & Sync)
        public virtual async Task<TEntity?> GetByIdAsync(object id)
            => await _dbSet.FindAsync(id);

        public virtual TEntity? GetById(object id)
            => _dbSet.Find(id);

        // Paging (Async & Sync)
        public virtual async Task<List<TEntity>> GetPagedAsync(int pageIndex, int pageSize)
            => await _dbSet.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        public virtual List<TEntity> GetPaged(int pageIndex, int pageSize)
            => _dbSet.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

        // Paging + filter
        public virtual async Task<List<TEntity>> FindPagedAsync(Expression<Func<TEntity, bool>> filter, int pageIndex, int pageSize)
            => await _dbSet.Where(filter).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

        public virtual List<TEntity> FindPaged(Expression<Func<TEntity, bool>> filter, int pageIndex, int pageSize)
            => _dbSet.Where(filter).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();

        // Count (Async & Sync)
        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null)
            => filter == null ? await _dbSet.CountAsync() : await _dbSet.CountAsync(filter!);

        public virtual int Count(Expression<Func<TEntity, bool>>? filter = null)
            => filter == null ? _dbSet.Count() : _dbSet.Count(filter!);

        // Add/Update/Remove
        public virtual void Add(TEntity entity) => _dbSet.Add(entity);
        public virtual void AddRange(IEnumerable<TEntity> entities) => _dbSet.AddRange(entities);
        public virtual void Remove(TEntity entity) => _dbSet.Remove(entity);
        public virtual void RemoveRange(IEnumerable<TEntity> entities) => _dbSet.RemoveRange(entities);
        public virtual void Update(TEntity entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
        }

        // Add/Update/Remove (Async)
        // Add single entity asynchronously
        public virtual async Task AddAsync(TEntity entity)
        {
            await _dbSet.AddAsync(entity);
        }

        // Add multiple entities asynchronously
        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        // Remove single entity asynchronously (EF Core không có remove async, nhưng bạn có thể dùng Task.Run nếu cần)
        public virtual Task RemoveAsync(TEntity entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        // Remove multiple entities asynchronously
        public virtual Task RemoveRangeAsync(IEnumerable<TEntity> entities)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        // Update asynchronously (Attach và Modified là sync, nên cũng bọc lại tương tự)
        public virtual Task UpdateAsync(TEntity entity)
        {
            _dbSet.Attach(entity);
            _context.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        //----------------------------------------------------------------------------
        //----------------------------------------------------------------------------
        // 1. GetAllIncludeAsync
        public virtual async Task<List<TEntity>> GetAllIncludeAsync(
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = _dbSet;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.ToListAsync();
        }

        // 2. FindIncludeAsync
        public virtual async Task<List<TEntity>> FindIncludeAsync(
            Expression<Func<TEntity, bool>> filter,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = _dbSet.Where(filter);
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.ToListAsync();
        }

        // 3. ExistsAsync
        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter)
        {
            return await _dbSet.AnyAsync(filter);
        }

        // 4. FirstOrDefaultAsync
        public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> filter)
        {
            return await _dbSet.FirstOrDefaultAsync(filter);
        }

        // 5. FirstOrDefault (Sync)
        public virtual TEntity? FirstOrDefault(Expression<Func<TEntity, bool>> filter)
        {
            return _dbSet.FirstOrDefault(filter);
        }


        public virtual async Task<IList<TEntity>> GetAllAsync(Expression<Func<IQueryable<TEntity>, IQueryable<TEntity>>>? include)
        {
            IQueryable<TEntity> query = _dbSet;

            if (include != null)
            {
                query = include.Compile()(query);
            }

            return await query.ToListAsync();
        }
    }
}

