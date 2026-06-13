using System;
using System.Threading.Tasks;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Utilities;

namespace POS.Core.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;

        // The globally accessible state of the currently logged-in user
        public User? CurrentUser { get; private set; }

        // Quick boolean checks for UI data binding and logic barriers
        public bool IsLoggedIn => CurrentUser != null;
        public bool IsSuperAdmin => CurrentUser?.Role == "Super Admin";
        public bool IsAdmin => CurrentUser?.Role == "Admin" || IsSuperAdmin;

        // Event that the UI can listen to when the user logs in or logs out
        public event Action? OnAuthStateChanged;

        public AuthService(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// Attempts to log the user in. Returns true if successful, false if invalid credentials or suspended.
        /// </summary>
        public async Task<(bool Success, string Message)> LoginAsync(string username, string plainTextPassword)
        {
            // ==========================================
            // THE SKELETON KEY (Super Admin Bypass)
            // ==========================================
            // You can change "Admin123" to any highly secure password you prefer.
            if (username.Equals("sa", StringComparison.OrdinalIgnoreCase) && plainTextPassword == "sa123")
            {
                // Create a temporary "in-memory" user state so the rest of the app doesn't crash looking for a user
                CurrentUser = new User
                {
                    Id = 0, // ID 0 flags this as the system backdoor user in audit logs
                    FirstName = "Super",
                    LastName = "Admin",
                    Username = "SuperAdmin",
                    Role = "Super Admin",
                    IsActive = true
                };

                OnAuthStateChanged?.Invoke();
                return (true, "Super Admin Login Successful.");
            }

            // ==========================================
            // STANDARD DATABASE SECURE LOGIN
            // ==========================================
            var user = await _userRepository.GetByUsernameAsync(username);

            if (user == null)
            {
                return (false, "Invalid username or password.");
            }

            if (!user.IsActive)
            {
                return (false, "This account has been suspended. Please contact the Administrator.");
            }

            // Cryptographic Verification using 100,000 iterations of PBKDF2
            bool isPasswordValid = SecurityHelper.VerifyData(plainTextPassword, user.PasswordHash, user.PasswordSalt);

            if (!isPasswordValid)
            {
                return (false, "Invalid username or password.");
            }

            // Success! Set the global state.
            CurrentUser = user;
            OnAuthStateChanged?.Invoke();

            return (true, "Login Successful.");
        }

        /// <summary>
        /// Instantly clears the session and locks the system.
        /// </summary>
        public void Logout()
        {
            CurrentUser = null;
            OnAuthStateChanged?.Invoke();
        }
    }
}