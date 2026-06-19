using POS.Cashier.UI.ViewModels.Dialogs;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class RedeemVoucherModalWindow : Window
    {
        public RedeemVoucherModalViewModel ViewModel { get; }

        public RedeemVoucherModalWindow(RedeemVoucherModalViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            // When the ViewModel successfully processes the voucher, tell the Window to close and return 'True'
            ViewModel.OnVoucherApproved = (voucherId, barcode, amount) =>
            {
                this.DialogResult = true;
                this.Close();
            };

            // When the user clicks Cancel, tell the Window to close and return 'False'
            ViewModel.OnCancel = () =>
            {
                this.DialogResult = false;
                this.Close();
            };
        }
    }
}