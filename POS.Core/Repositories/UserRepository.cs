using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class UserRepository
    {
        public const int MaximumFailedLoginAttempts = 5;
        public const int LockoutMinutes = 15;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public UserRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<bool> AnyUsersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Users.AsNoTracking().AnyAsync();
        }

        public async Task<bool> CreateFirstAdministratorAsync(User administrator)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction =
                await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            if (await context.Users.AnyAsync())
            {
                return false;
            }

            context.Users.Add(administrator);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }

        public async Task<IEnumerable<User>> GetAllAsync(
            string searchTerm = "",
            string roleFilter = "All Roles")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string normalizedSearch = searchTerm.Trim().ToLower();

                query = query.Where(user =>
                    user.FirstName.ToLower().Contains(normalizedSearch) ||
                    user.LastName.ToLower().Contains(normalizedSearch) ||
                    user.EmployeeId.ToLower().Contains(normalizedSearch) ||
                    user.Username.ToLower().Contains(normalizedSearch));
            }

            if (roleFilter != "All Roles" &&
                Enum.TryParse<UserRole>(roleFilter, out var parsedRole))
            {
                query = query.Where(user => user.Role == parsedRole);
            }

            return await query
                .OrderByDescending(user => user.IsActive)
                .ThenBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string normalizedUsername = username.Trim().ToLower();

            return await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user =>
                    user.Username.ToLower() == normalizedUsername);
        }

        public async Task<bool> IsUsernameUniqueAsync(
            string username,
            int currentUserId = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string normalizedUsername = username.Trim().ToLower();

            return !await context.Users.AnyAsync(user =>
                user.Username.ToLower() == normalizedUsername &&
                user.Id != currentUserId);
        }

        public async Task<int> CountActiveAdministratorsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Users.CountAsync(user =>
                user.Role == UserRole.Admin &&
                user.IsActive);
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
            await using var transaction =
                await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var existingUser = await context.Users
                .FirstOrDefaultAsync(existing => existing.Id == user.Id);

            if (existingUser == null)
            {
                throw new InvalidOperationException(
                    "The selected user no longer exists.");
            }

            bool wasActiveAdministrator =
                existingUser.Role == UserRole.Admin &&
                existingUser.IsActive;

            bool willRemainActiveAdministrator =
                user.Role == UserRole.Admin &&
                user.IsActive;

            if (wasActiveAdministrator && !willRemainActiveAdministrator)
            {
                int activeAdministratorCount = await context.Users.CountAsync(
                    candidate =>
                        candidate.Role == UserRole.Admin &&
                        candidate.IsActive);

                if (activeAdministratorCount <= 1)
                {
                    throw new InvalidOperationException(
                        "The last active Administrator cannot be suspended or assigned another role. Create or activate another Administrator first.");
                }
            }

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.EmployeeId = user.EmployeeId;
            existingUser.Mobile = user.Mobile;
            existingUser.Username = user.Username;
            existingUser.Role = user.Role;
            existingUser.IsActive = user.IsActive;

            if (!string.IsNullOrWhiteSpace(user.PasswordHash) &&
                !string.IsNullOrWhiteSpace(user.PasswordSalt))
            {
                existingUser.PasswordHash = user.PasswordHash;
                existingUser.PasswordSalt = user.PasswordSalt;

                // A deliberate password change also clears an old lock.
                existingUser.FailedLoginAttempts = 0;
                existingUser.LockoutEndUtc = null;
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<(int FailedAttempts, DateTime? LockoutEndUtc)>
            RegisterFailedLoginAsync(
                int userId,
                string username,
                string applicationName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction =
                await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var user = await context.Users
                .FirstOrDefaultAsync(candidate => candidate.Id == userId);

            if (user == null)
            {
                await AddLoginAuditInternalAsync(
                    context,
                    null,
                    username,
                    "Failure",
                    applicationName,
                    "Invalid username or password.");

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (0, null);
            }

            DateTime utcNow = DateTime.UtcNow;

            if (user.LockoutEndUtc.HasValue &&
                user.LockoutEndUtc.Value <= utcNow)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEndUtc = null;
            }

            user.FailedLoginAttempts++;

            string eventType = "Failure";
            string message = "Invalid username or password.";

            if (user.FailedLoginAttempts >= MaximumFailedLoginAttempts)
            {
                user.LockoutEndUtc = utcNow.AddMinutes(LockoutMinutes);
                eventType = "Locked";
                message =
                    $"Account locked for {LockoutMinutes} minutes after repeated failed login attempts.";
            }

            await AddLoginAuditInternalAsync(
                context,
                user.Id,
                username,
                eventType,
                applicationName,
                message);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            return (user.FailedLoginAttempts, user.LockoutEndUtc);
        }

        public async Task RecordSuccessfulLoginAsync(
            int userId,
            string username,
            string applicationName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction =
                await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var user = await context.Users
                .FirstOrDefaultAsync(candidate => candidate.Id == userId);

            if (user == null)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;

            user.FailedLoginAttempts = 0;
            user.LockoutEndUtc = null;
            user.LastLoginAtUtc = utcNow;

            await AddLoginAuditInternalAsync(
                context,
                user.Id,
                username,
                "Success",
                applicationName,
                "Login successful.");

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task RecordLoginAuditAsync(
            int? userId,
            string username,
            string eventType,
            string applicationName,
            string message)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            await AddLoginAuditInternalAsync(
                context,
                userId,
                username,
                eventType,
                applicationName,
                message);

            await context.SaveChangesAsync();
        }

        private static Task AddLoginAuditInternalAsync(
            AppDbContext context,
            int? userId,
            string username,
            string eventType,
            string applicationName,
            string message)
        {
            var auditEvent = new LoginAuditEvent
            {
                UserId = userId,
                UsernameAttempted = (username ?? string.Empty).Trim(),
                EventType = eventType,
                ApplicationName = string.IsNullOrWhiteSpace(applicationName)
                    ? "POS"
                    : applicationName.Trim(),
                MachineName = Environment.MachineName,
                Message = message,
                EventTimeUtc = DateTime.UtcNow
            };

            return context.LoginAuditEvents.AddAsync(auditEvent).AsTask();
        }
    }
}
