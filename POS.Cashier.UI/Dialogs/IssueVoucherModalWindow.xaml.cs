using POS.Cashier.UI.ViewModels;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class IssueVoucherModalWindow : Window
    {
        public IssueVoucherModalViewModel ViewModel { get; }

        public IssueVoucherModalWindow(IssueVoucherModalViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            // When the cashier successfully scans a card and adds it to the cart
            ViewModel.OnVoucherAddedToCart = (barcode, amount, customerName) =>
            {
                this.DialogResult = true;
                this.Close();
            };

            // When the cashier clicks cancel
            ViewModel.OnCancel = () =>
            {
                this.DialogResult = false;
                this.Close();
            };
        }
    }
}