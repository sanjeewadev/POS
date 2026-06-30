using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class GiftVoucherTenderDialog : Window
    {
        public GiftVoucherTenderDialogViewModel? ViewModel { get; private set; }

        public int GiftVoucherId => ViewModel?.GiftVoucherId ?? 0;

        public string VoucherNo => ViewModel?.VoucherNo ?? string.Empty;

        public string VoucherBarcode => ViewModel?.VoucherBarcode ?? string.Empty;

        public decimal VoucherAmount => ViewModel?.VoucherAmount ?? 0m;

        public decimal AmountToApply => ViewModel?.AmountToApply ?? 0m;

        public decimal ForfeitedAmount => ViewModel?.ForfeitedAmount ?? 0m;

        public GiftVoucherTenderDialog(decimal balanceDue)
        {
            InitializeComponent();

            if (App.Services != null)
            {
                ViewModel = App.Services.GetRequiredService<GiftVoucherTenderDialogViewModel>();
                ViewModel.Initialize(balanceDue);

                DataContext = ViewModel;
                ViewModel.ActionCompleted += OnActionCompleted;
            }
            else
            {
                MessageBox.Show(
                    "Application services are not available.",
                    "Gift Voucher Payment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public GiftVoucherTenderDialog(GiftVoucherTenderDialogViewModel viewModel, decimal balanceDue)
        {
            InitializeComponent();

            ViewModel = viewModel;
            ViewModel.Initialize(balanceDue);

            DataContext = ViewModel;
            ViewModel.ActionCompleted += OnActionCompleted;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeTextBox.Focus();
            BarcodeTextBox.SelectAll();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (e.Key == Key.Escape)
            {
                ViewModel.CancelCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                HandleEnterKey();
                e.Handled = true;
            }
        }

        private void HandleEnterKey()
        {
            if (ViewModel == null)
                return;

            if (Keyboard.FocusedElement is TextBox textBox && textBox == BarcodeTextBox)
            {
                ViewModel.SearchVoucherCommand.Execute(null);
                return;
            }

            if (ViewModel.CanConfirm)
            {
                ViewModel.ConfirmCommand.Execute(null);
                return;
            }

            ViewModel.SearchVoucherCommand.Execute(null);
        }

        private void OnActionCompleted(bool success)
        {
            DialogResult = success;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (ViewModel != null)
                ViewModel.ActionCompleted -= OnActionCompleted;

            base.OnClosed(e);
        }
    }
}