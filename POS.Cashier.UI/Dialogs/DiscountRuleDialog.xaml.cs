using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.Models;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class DiscountRuleDialog : Window
    {
        private readonly CartItem _selectedItem;
        private readonly string _customerType;
        private readonly bool _isManagerModeActive;
        private readonly string _approvedBy;

        public DiscountRuleDialogViewModel? ViewModel => DataContext as DiscountRuleDialogViewModel;

        public DiscountRuleApplyResult? Result => ViewModel?.Result;

        public DiscountRuleDialog(
            CartItem selectedItem,
            string customerType = "Walk-In",
            bool isManagerModeActive = false,
            string approvedBy = "")
        {
            InitializeComponent();

            _selectedItem = selectedItem ?? throw new ArgumentNullException(nameof(selectedItem));
            _customerType = string.IsNullOrWhiteSpace(customerType) ? "Walk-In" : customerType.Trim();
            _isManagerModeActive = isManagerModeActive;
            _approvedBy = string.IsNullOrWhiteSpace(approvedBy) ? string.Empty : approvedBy.Trim();

            DataContext = App.Services!.GetRequiredService<DiscountRuleDialogViewModel>();

            Loaded += DiscountRuleDialog_Loaded;
            Unloaded += DiscountRuleDialog_Unloaded;
        }

        private async void DiscountRuleDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
                return;

            ViewModel.RequestClose += ViewModel_RequestClose;

            await ViewModel.InitializeAsync(
                _selectedItem,
                _customerType,
                _isManagerModeActive,
                _approvedBy);
        }

        private void DiscountRuleDialog_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.RequestClose -= ViewModel_RequestClose;
        }

        private void ViewModel_RequestClose(bool? result)
        {
            DialogResult = result;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
                Close();
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                if (ViewModel.CanConfirm)
                    await ViewModel.ConfirmAsync();
            }
        }
    }
}