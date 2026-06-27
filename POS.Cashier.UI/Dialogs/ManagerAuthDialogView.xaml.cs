using System;
using System.Windows;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ManagerAuthDialogView : Window
    {
        public ManagerAuthDialogView(ManagerAuthViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;

            // Wire up the event so the ViewModel can tell the Window when to close itself
            viewModel.AuthenticationCompleted += OnAuthenticationCompleted;
        }

        private void OnAuthenticationCompleted(bool success)
        {
            if (success)
            {
                // Returns 'true' to whatever button opened this dialog
                this.DialogResult = true;
                this.Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            // Returns 'false', so the system knows the override was aborted
            this.DialogResult = false;
            this.Close();
        }

        // CRITICAL: We must un-subscribe from the event when the window closes 
        // to prevent the dreaded WPF Memory Leak!
        protected override void OnClosed(EventArgs e)
        {
            if (this.DataContext is ManagerAuthViewModel viewModel)
            {
                viewModel.AuthenticationCompleted -= OnAuthenticationCompleted;
            }
            base.OnClosed(e);
        }
    }
}