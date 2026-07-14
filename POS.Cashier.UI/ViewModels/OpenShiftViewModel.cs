using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class OpenShiftViewModel :
        ObservableObject
    {
        private readonly TillRepository _tillRepository;

        [ObservableProperty]
        private string _terminalNo = string.Empty;

        [ObservableProperty]
        private string _cashierName = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public ShiftSession? CreatedShift { get; private set; }

        public OpenShiftViewModel(
            TillRepository tillRepository)
        {
            _tillRepository = tillRepository;
        }

        public void Initialize(
            string terminalNo,
            string cashierName)
        {
            TerminalNo =
                (terminalNo ?? string.Empty).Trim();

            CashierName =
                (cashierName ?? string.Empty).Trim();

            ErrorMessage = string.Empty;
            CreatedShift = null;
        }

        public async Task<bool> OpenShiftAsync()
        {
            if (IsBusy)
                return false;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(TerminalNo))
            {
                ErrorMessage =
                    "Terminal number is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(CashierName))
            {
                ErrorMessage =
                    "Cashier name is required.";
                return false;
            }

            try
            {
                IsBusy = true;

                CreatedShift =
                    await _tillRepository
                        .CreateNewShiftAsync(
                            TerminalNo,
                            CashierName,
                            0m);

                return true;
            }
            catch (Exception ex)
            {
                CreatedShift = null;
                ErrorMessage =
                    $"Shift could not be opened: {ex.Message}";
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
