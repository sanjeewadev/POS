using System;
using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CashMovementDialogView : Window
    {
        public CashMovementDialogView(CashMovementViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
            viewModel.ActionCompleted += OnActionCompleted;
        }

        private void OnActionCompleted(bool success)
        {
            DialogResult = success;

            if (success)
                Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag errors when mouse capture is lost.
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is CashMovementViewModel viewModel)
                viewModel.ActionCompleted -= OnActionCompleted;

            base.OnClosed(e);
        }
    }
}