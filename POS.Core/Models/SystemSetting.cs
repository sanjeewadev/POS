using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class SystemSetting
    {
        [Key]
        [MaxLength(100)]
        public string SettingKey { get; set; } = string.Empty; // e.g., "GlobalVatRate"

        public string SettingValue { get; set; } = string.Empty; // e.g., "15.00"

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}