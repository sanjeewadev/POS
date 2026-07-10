using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class LoginAuditEvent
    {
        public int Id { get; set; }

        public int? UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string UsernameAttempted { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string ApplicationName { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string MachineName { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Message { get; set; } = string.Empty;

        public DateTime EventTimeUtc { get; set; }
    }
}
