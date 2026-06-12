//using Microsoft.EntityFrameworkCore;
//using POS.Application.Interfaces;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//namespace POS.Infrastructure.Data.Repositories
//{
//    public class Repository<T> : IRepository<T> where T : class
//    {
//        protected readonly AppDbContext _context;
//        private readonly DbSet<T> _dbSet;

//        public Repository(AppDbContext context)
//        {
//            _context = context;
//            _dbSet = context.Set<T>();
//        }

//        public async Task<T?> GetByIdAsync(int id)
//        {
//            return await _dbSet.FindAsync(id);
//        }

//        public async Task<IEnumerable<T>> GetAllAsync()
//        {
//            return await _dbSet.ToListAsync();
//        }

//        public async Task AddAsync(T entity)
//        {
//            await _dbSet.AddAsync(entity);
//            await _context.SaveChangesAsync();
//        }

//        public async Task UpdateAsync(T entity)
//        {
//            _dbSet.Update(entity);
//            await _context.SaveChangesAsync();
//        }

//        public async Task DeleteAsync(T entity)
//        {
//            _dbSet.Remove(entity);
//            await _context.SaveChangesAsync();
//        }
//    }
//}