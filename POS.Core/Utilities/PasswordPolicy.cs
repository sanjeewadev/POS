using System;
using System.Linq;

namespace POS.Core.Utilities
{
    public static class PasswordPolicy
    {
        public const int MinimumLength = 4;
        public const int MaximumLength = 64;

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
                return (
                    false,
                    $"Password must contain at least {MinimumLength} characters.");
            }

            if (password.Length > MaximumLength)
            {
                return (
                    false,
                    $"Password cannot exceed {MaximumLength} characters.");
            }

            if (password.Any(char.IsWhiteSpace))
            {
                return (false, "Password cannot contain spaces.");
            }

            if (!password.Any(char.IsLetter))
            {
                return (false, "Password must contain at least one letter.");
            }

            if (!password.Any(char.IsDigit))
            {
                return (false, "Password must contain at least one number.");
            }

            if (!string.IsNullOrWhiteSpace(username) &&
                password.Equals(username, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Password cannot be the same as the username.");
            }

            return (true, string.Empty);
        }
    }
}
