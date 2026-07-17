using System;
using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ManagerAuthDialogView : Window
    {
        public ManagerAuthDialogView(ManagerAuthViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            viewModel.AuthenticationCompleted += OnAuthenticationCompleted;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UsernameBox.Focus();
            UsernameBox.SelectAll();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private void OnAuthenticationCompleted(bool success)
        {
            if (success)
                DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is ManagerAuthViewModel viewModel)
                viewModel.AuthenticationCompleted -= OnAuthenticationCompleted;

            base.OnClosed(e);
        }
    }
}
