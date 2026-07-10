using System;
using System.ComponentModel.DataAnnotations;
using POS.Core.Enums;

namespace POS.Core.Models
{
    public class User
    {
        public int Id { get; set; }

        // --- HUMAN RESOURCES DATA ---
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string EmployeeId { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Mobile { get; set; } = string.Empty;

        // --- AUTHENTICATION ---
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string PasswordSalt { get; set; } = string.Empty;

        public int FailedLoginAttempts { get; set; }

        public DateTime? LockoutEndUtc { get; set; }

        public DateTime? LastLoginAtUtc { get; set; }

        // --- AUTHORIZATION ---
        [Required]
        public UserRole Role { get; set; } = UserRole.Cashier;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // UI helper properties.
        public string FullName => $"{FirstName} {LastName}".Trim();

        public string StatusText => IsActive ? "Active" : "Suspended";
    }
}
