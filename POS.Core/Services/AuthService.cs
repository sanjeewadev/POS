using System;
using System.Threading.Tasks;
using POS.Core.Enums;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Utilities;

namespace POS.Core.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;

        public User? CurrentUser { get; private set; }

        public bool IsLoggedIn => CurrentUser != null;

        public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;

        public bool IsManager =>
            CurrentUser?.Role == UserRole.Manager ||
            CurrentUser?.Role == UserRole.Admin;

        public event Action? OnAuthStateChanged;

        public AuthService(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<(bool Success, string Message)> LoginAsync(
            string username,
            string plainTextPassword,
            string applicationName = "POS")
        {
            string normalizedUsername = (username ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedUsername) ||
                string.IsNullOrWhiteSpace(plainTextPassword))
            {
                return (false, "Username and password are required.");
            }

            var user =
                await _userRepository.GetByUsernameAsync(normalizedUsername);

            if (user == null)
            {
                await _userRepository.RecordLoginAuditAsync(
                    null,
                    normalizedUsername,
                    "Failure",
                    applicationName,
                    "Invalid username or password.");

                return (false, "Invalid username or password.");
            }

            if (!user.IsActive)
            {
                await _userRepository.RecordLoginAuditAsync(
                    user.Id,
                    normalizedUsername,
                    "Suspended",
                    applicationName,
                    "Login refused because the account is suspended.");

                return (
                    false,
                    "This account has been suspended. Please contact an Administrator.");
            }

            DateTime utcNow = DateTime.UtcNow;

            if (user.LockoutEndUtc.HasValue &&
                user.LockoutEndUtc.Value > utcNow)
            {
                int remainingMinutes = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        (user.LockoutEndUtc.Value - utcNow).TotalMinutes));

                await _userRepository.RecordLoginAuditAsync(
                    user.Id,
                    normalizedUsername,
                    "Locked",
                    applicationName,
                    "Login refused because the temporary lockout is active.");

                return (
                    false,
                    $"This account is temporarily locked. Try again in about {remainingMinutes} minute(s).");
            }

            bool isPasswordValid = SecurityHelper.VerifyData(
                plainTextPassword,
                user.PasswordHash,
                user.PasswordSalt);

            if (!isPasswordValid)
            {
                var failure = await _userRepository.RegisterFailedLoginAsync(
                    user.Id,
                    normalizedUsername,
                    applicationName);

                if (failure.LockoutEndUtc.HasValue &&
                    failure.LockoutEndUtc.Value > DateTime.UtcNow)
                {
                    return (
                        false,
                        $"Too many failed attempts. This account is locked for {UserRepository.LockoutMinutes} minutes.");
                }

                return (false, "Invalid username or password.");
            }

            await _userRepository.RecordSuccessfulLoginAsync(
                user.Id,
                normalizedUsername,
                applicationName);

            // Refresh the user so CurrentUser has the latest login state.
            CurrentUser =
                await _userRepository.GetByUsernameAsync(normalizedUsername);

            OnAuthStateChanged?.Invoke();

            return (true, "Login successful.");
        }


        public async Task<ManagerAuthorizationResult> ValidateManagerCredentialsAsync(
            string username,
            string plainTextPassword,
            string applicationName = "POS-Cashier-Approval")
        {
            string normalizedUsername = (username ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedUsername) ||
                string.IsNullOrWhiteSpace(plainTextPassword))
            {
                return new ManagerAuthorizationResult
                {
                    Success = false,
                    Message = "Username and password are required."
                };
            }

            User? user = await _userRepository.GetByUsernameAsync(normalizedUsername);

            if (user == null)
            {
                await _userRepository.RecordLoginAuditAsync(
                    null,
                    normalizedUsername,
                    "ApprovalFailure",
                    applicationName,
                    "Invalid manager username or password.");

                return new ManagerAuthorizationResult
                {
                    Success = false,
                    Message = "Invalid username or password."
                };
            }

            if (!user.IsActive)
            {
                await _userRepository.RecordLoginAuditAsync(
                    user.Id,
                    normalizedUsername,
                    "ApprovalSuspended",
                    applicationName,
                    "Manager approval refused because the account is suspended.");

                return new ManagerAuthorizationResult
                {
                    Success = false,
                    Message = "This account has been suspended."
                };
            }

            DateTime utcNow = DateTime.UtcNow;

            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > utcNow)
            {
                await _userRepository.RecordLoginAuditAsync(
                    user.Id,
                    normalizedUsername,
                    "ApprovalLocked",
                    applicationName,
                    "Manager approval refused because the temporary lockout is active.");

                return new ManagerAuthorizationResult
                {
                    Success = false,
                    Message = "This account is temporarily locked."
                };
            }

            bool validPassword = SecurityHelper.VerifyData(
                plainTextPassword,
                user.PasswordHash,
                user.PasswordSalt);

            if (!validPassword)
            {
                var failure = await _userRepository.RegisterFailedLoginAsync(
                    user.Id,
                    normalizedUsername,
                    applicationName);

                return new ManagerAuthorizationResult
                {
                    Success = false,
                    Message = failure.LockoutEndUtc.HasValue
                        ? $"Too many failed attempts. This account is locked for {UserRepository.LockoutMinutes} minutes."
                        : "Invalid username or password."
                };
            }

            if (user.Role != UserRole.Manager && user.Role != UserRole.Admin)
            {
                await _userRepository.RecordLoginAuditAsync(
                    user.Id,
                    normalizedUsername,
                    "ApprovalDenied",
                    applicationName,
                    "Valid credentials but Manager or Administrator role is required.");

                return new ManagerAuthorizationResult
                {
                    Success = false,
                    Message = "Manager or Administrator privileges are required."
                };
            }

            await _userRepository.RecordLoginAuditAsync(
                user.Id,
                normalizedUsername,
                "ApprovalSuccess",
                applicationName,
                "Manager authorization successful without changing the active cashier session.");

            return new ManagerAuthorizationResult
            {
                Success = true,
                Message = "Authorization successful.",
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role
            };
        }

        public Task RecordSecurityAuditAsync(
            int? userId,
            string username,
            string eventType,
            string applicationName,
            string message)
        {
            return _userRepository.RecordLoginAuditAsync(
                userId,
                username,
                eventType,
                applicationName,
                message);
        }

        public void Logout()
        {
            CurrentUser = null;
            OnAuthStateChanged?.Invoke();
        }
    }
}
