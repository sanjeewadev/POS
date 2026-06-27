using System;
using System.Windows;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class FloatCashDialog : Window
    {
        public FloatCashDialog(FloatCashViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;

            // Listens for the successful database execution to close itself
            viewModel.ActionCompleted += OnActionCompleted;
        }

        private void OnActionCompleted(bool success)
        {
            if (success)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Prevent memory leaks!
        protected override void OnClosed(EventArgs e)
        {
            if (this.DataContext is FloatCashViewModel viewModel)
            {
                viewModel.ActionCompleted -= OnActionCompleted;
            }
            base.OnClosed(e);
        }
    }
}