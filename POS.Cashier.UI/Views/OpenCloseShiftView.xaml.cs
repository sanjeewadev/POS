using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Views
{
    public partial class OpenCloseShiftView : Window
    {
        private bool _isAuthorizedClose = false;

        // UPDATED CONSTRUCTOR: Now accepts the mode!
        public OpenCloseShiftView(ShiftMode mode)
        {
            InitializeComponent();

            if (App.Services != null)
            {
                var vm = App.Services.GetRequiredService<OpenCloseShiftViewModel>();
                vm.Initialize(mode); // Tell the brain what mode we are in
                this.DataContext = vm;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _isAuthorizedClose = true;
            this.DialogResult = false;
            this.Close();
        }

        private async void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is OpenCloseShiftViewModel vm)
            {
                // Calls our new routing method!
                bool success = await vm.ProcessShiftAsync();
                if (!success) return;
            }

            _isAuthorizedClose = true;
            this.DialogResult = true;
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isAuthorizedClose)
            {
                e.Cancel = true;
                MessageBox.Show("SECURITY LOCKOUT:\n\nYou must explicitly confirm your cash count or click 'CANCEL'.", "Action Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}