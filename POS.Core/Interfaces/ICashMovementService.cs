using System.Threading.Tasks;
using POS.Core.Models;

namespace POS.Core.Interfaces
{
    public interface ICashMovementService
    {
        Task<bool> VerifyManagerPinAsync(string pin);
        Task<decimal> GetCurrentDrawerBalanceAsync(int shiftSessionId);
        Task<CashMovement> ProcessMovementAsync(int shiftSessionId, string movementType, decimal amount, string reasonCategory, string remarks, string cashierName, string authorizedBy);
    }
}