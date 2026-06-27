using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.Messages;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.Cashier.UI.ViewModels
{
    public partial class CashMovementViewModel : ObservableObject
    {
        private readonly TillRepository _tillRepository;
        private int _shiftId;
        private string _cashierName = string.Empty;

        [ObservableProperty] private string _movementType = string.Empty;
        [ObservableProperty] private decimal _amount = 0m;

        // --- DYNAMIC UI PROPERTIES ---
        [ObservableProperty] private string _headerTitle = string.Empty;
        [ObservableProperty] private string _themeColorHex = "#003366";
        [ObservableProperty] private string _buttonText = string.Empty;

        [ObservableProperty] private string _selectedReason = string.Empty;
        [ObservableProperty] private string _remarks = string.Empty;

        public ObservableCollection<string> ReasonCategories { get; } = new();

        public event Action<bool>? ActionCompleted;

        public CashMovementViewModel(TillRepository tillRepository)
        {
            _tillRepository = tillRepository;
        }

        public void Initialize(string movementType, decimal amount, int shiftId, string cashierName)
        {
            _shiftId = shiftId;
            _cashierName = cashierName;
            MovementType = movementType;
            Amount = amount;

            ReasonCategories.Clear();

            // DYNAMIC THEME ENGINE
            if (movementType == "Paid In")
            {
                HeaderTitle = "PAID IN — RECEIVE CASH";
                ThemeColorHex = "#10B981"; // Emerald Green
                ButtonText = "CONFIRM PAID IN";

                ReasonCategories.Add("Change / Coin Exchange");
                ReasonCategories.Add("Customer Account Payment");
                ReasonCategories.Add("Other Income");
            }
            else
            {
                HeaderTitle = "PAID OUT — REMOVE CASH";
                ThemeColorHex = "#EF4444"; // Ruby Red
                ButtonText = "CONFIRM PAID OUT";

                ReasonCategories.Add("Vendor / Supplier Payment");
                ReasonCategories.Add("Store Expenses");
                ReasonCategories.Add("Petty Cash");
                ReasonCategories.Add("Owner Draw");
            }
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedReason))
            {
                MessageBox.Show("Please select a reason for this cash movement.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _tillRepository.RegisterCashMovementAsync(_shiftId, MovementType, Amount, SelectedReason, Remarks, _cashierName);

                // Send silent Walkie-Talkie notification to the top bar
                WeakReferenceMessenger.Default.Send(new TopBarNotificationMessage(
                    ($"{MovementType}: Rs. {Amount:N2}", ThemeColorHex)));

                // Open Cash Drawer Trigger can go here later!

                ActionCompleted?.Invoke(true);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Overdraft Prevented", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"System Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}