using System;
using System.Security.Cryptography;

namespace POS.Core.Utilities
{
    public static class SecurityHelper
    {
        // 128-bit salt and 256-bit key
        private const int SaltSize = 16;
        private const int KeySize = 32;

        // High iteration count artificially slows down the algorithm to defeat brute-force attacks
        private const int Iterations = 100000;
        private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;

        /// <summary>
        /// Hashes a plain text string (Password or PIN) and generates a unique salt.
        /// </summary>
        public static string HashData(string plainText, out string salt)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                salt = string.Empty;
                return string.Empty;
            }

            // 1. Generate a random Salt
            byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
            salt = Convert.ToBase64String(saltBytes);

            // 2. Hash the data combining the plain text and the salt
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                System.Text.Encoding.UTF8.GetBytes(plainText),
                saltBytes,
                Iterations,
                _hashAlgorithmName,
                KeySize);

            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verifies an attempted login string against the stored Hash and Salt.
        /// </summary>
        public static bool VerifyData(string plainTextAttempt, string storedHash, string storedSalt)
        {
            if (string.IsNullOrWhiteSpace(plainTextAttempt) || string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
                return false;

            try
            {
                byte[] saltBytes = Convert.FromBase64String(storedSalt);

                // Hash the incoming attempt using the same exact parameters
                var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
                    System.Text.Encoding.UTF8.GetBytes(plainTextAttempt),
                    saltBytes,
                    Iterations,
                    _hashAlgorithmName,
                    KeySize);

                // FixedTimeEquals prevents timing attacks where hackers measure how long the string comparison takes
                return CryptographicOperations.FixedTimeEquals(hashToCompare, Convert.FromBase64String(storedHash));
            }
            catch
            {
                return false;
            }
        }
    }
}