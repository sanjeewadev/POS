using POS.Core.Enums;

namespace POS.Core.Services
{
    public sealed class ManagerAuthorizationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public int? UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public UserRole? Role { get; init; }
    }
}
