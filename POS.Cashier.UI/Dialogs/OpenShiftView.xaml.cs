using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class OpenShiftView : Window
    {
        public OpenShiftView()
        {
            InitializeComponent();
            PreviewKeyDown += OpenShiftView_PreviewKeyDown;
        }

        private async void OpenShift_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (DataContext is not
                OpenShiftViewModel viewModel)
            {
                return;
            }

            bool success =
                await viewModel.OpenShiftAsync();

            if (success)
                DialogResult = true;
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OpenShiftView_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OpenShift_Click(
                    this,
                    new RoutedEventArgs());
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogResult = false;
            }
        }
    }
}
