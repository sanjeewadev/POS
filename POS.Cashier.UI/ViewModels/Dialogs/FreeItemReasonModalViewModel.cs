using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace POS.Cashier.UI.ViewModels.Dialogs
{
    public partial class FreeItemReasonModalViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _itemName = string.Empty;

        [ObservableProperty]
        private decimal _originalPrice;

        // This holds the result of their button tap
        public string SelectedReason { get; private set; } = string.Empty;

        // Callbacks to close the Window
        public Action? OnReasonConfirmed;
        public Action? OnCancel;

        public void Initialize(string itemName, decimal originalPrice)
        {
            ItemName = itemName;
            OriginalPrice = originalPrice;
            SelectedReason = string.Empty;
        }

        [RelayCommand]
        private void SelectReason(string reason)
        {
            SelectedReason = reason;
            // The moment they tap a reason, we instantly confirm and close the window
            OnReasonConfirmed?.Invoke();
        }

        [RelayCommand]
        private void Cancel() => OnCancel?.Invoke();
    }
}