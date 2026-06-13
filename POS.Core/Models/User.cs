using System;
using System.ComponentModel.DataAnnotations;

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

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Mobile { get; set; } = string.Empty;

        // --- AUTHENTICATION KEYS ---
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        // The secure mathematically derived hashes (NEVER plain text)
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string PasswordSalt { get; set; } = string.Empty;

        public string PosPinHash { get; set; } = string.Empty;

        public string PosPinSalt { get; set; } = string.Empty;

        // Lifecycle flags
        public bool ForcePasswordReset { get; set; } = true;

        // --- AUTHORIZATION ---
        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Cashier";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Helper property for the UI DataGrid
        public string FullName => $"{FirstName} {LastName}";

        // ADD THIS LINE:
        public string StatusText => IsActive ? "Active" : "Suspended";
    }
}