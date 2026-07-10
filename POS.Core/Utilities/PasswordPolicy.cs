using System;
using System.Linq;

namespace POS.Core.Utilities
{
    public static class PasswordPolicy
    {
        public const int MinimumLength = 12;
        public const int MaximumLength = 128;

        public static (bool IsValid, string ErrorMessage) Validate(
            string password,
            string username)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "Password is required.");
            }

            if (password.Length < MinimumLength)
            {
                return (false, $"Password must contain at least {MinimumLength} characters.");
            }

            if (password.Length > MaximumLength)
            {
                return (false, $"Password cannot exceed {MaximumLength} characters.");
            }

            if (password.Any(char.IsWhiteSpace))
            {
                return (false, "Password cannot contain spaces.");
            }

            if (!password.Any(char.IsUpper))
            {
                return (false, "Password must contain at least one uppercase letter.");
            }

            if (!password.Any(char.IsLower))
            {
                return (false, "Password must contain at least one lowercase letter.");
            }

            if (!password.Any(char.IsDigit))
            {
                return (false, "Password must contain at least one number.");
            }

            if (!password.Any(character => !char.IsLetterOrDigit(character)))
            {
                return (false, "Password must contain at least one special character.");
            }

            if (!string.IsNullOrWhiteSpace(username) &&
                password.Contains(username, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Password cannot contain the username.");
            }

            return (true, string.Empty);
        }
    }
}
