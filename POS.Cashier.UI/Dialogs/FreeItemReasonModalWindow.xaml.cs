using POS.Cashier.UI.ViewModels.Dialogs;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class FreeItemReasonModalWindow : Window
    {
        public FreeItemReasonModalViewModel ViewModel { get; }

        public FreeItemReasonModalWindow(FreeItemReasonModalViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;

            ViewModel.OnReasonConfirmed = () =>
            {
                this.DialogResult = true;
                this.Close();
            };

            ViewModel.OnCancel = () =>
            {
                this.DialogResult = false;
                this.Close();
            };
        }
    }
}