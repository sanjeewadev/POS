using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;
using POS.Core.Enums;

namespace POS.Core.Repositories
{
    public class UserRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public UserRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<User>> GetAllAsync(string searchTerm = "", string roleFilter = "All Roles")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearch = searchTerm.ToLower();
                query = query.Where(u =>
                    u.FirstName.ToLower().Contains(lowerSearch) ||
                    u.LastName.ToLower().Contains(lowerSearch) ||
                    u.EmployeeId.ToLower().Contains(lowerSearch));
            }

            // FIX: Parse the string filter into the strongly-typed UserRole enum
            if (roleFilter != "All Roles" && Enum.TryParse<UserRole>(roleFilter, out var parsedRole))
            {
                query = query.Where(u => u.Role == parsedRole);
            }

            // Order by Status (Active first), then by Name
            return await query.OrderByDescending(u => u.IsActive).ThenBy(u => u.FirstName).ToListAsync();
        }

        // Extremely fast lookup used ONLY by the Login screen
        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Users.AsNoTracking()
                                      .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        public async Task<bool> IsUsernameUniqueAsync(string username, int currentUserId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return !await context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower() && u.Id != currentUserId);
        }

        public async Task AddAsync(User user)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            context.Users.Update(user);
            await context.SaveChangesAsync();
        }
    }
}