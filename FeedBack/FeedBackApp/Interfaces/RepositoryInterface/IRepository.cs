using System.Linq.Expressions;

namespace FeedBackApp.Interfaces.RepositoryInterface
{
    public interface IRepository<T> where T : class
    {
        Task<T?>              GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<T?>              FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<bool>            AnyAsync(Expression<Func<T, bool>> predicate);
        Task<int>             CountAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T>         GetQueryable();
        Task                  AddAsync(T entity);
        Task                  AddRangeAsync(IEnumerable<T> entities);
        void                  Update(T entity);
        void                  Remove(T entity);
        void                  RemoveRange(IEnumerable<T> entities);
        Task                  SaveChangesAsync();
    }
}
