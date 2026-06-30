using System.Windows;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class PaymentDialog : Window
    {
        private readonly PaymentDialogViewModel _viewModel;

        public PaymentDialogViewModel ViewModel => _viewModel;

        public PaymentDialog(PaymentDialogViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ActionCompleted += ViewModel_ActionCompleted;

            Loaded += (_, _) =>
            {
                AmountTenderedTextBox.Focus();
                AmountTenderedTextBox.SelectAll();
            };

            Closed += (_, _) =>
            {
                _viewModel.ActionCompleted -= ViewModel_ActionCompleted;
            };
        }

        private void ViewModel_ActionCompleted(bool confirmed)
        {
            DialogResult = confirmed;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_viewModel.ConfirmCommand.CanExecute(null))
                    _viewModel.ConfirmCommand.Execute(null);

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (_viewModel.CancelCommand.CanExecute(null))
                    _viewModel.CancelCommand.Execute(null);

                e.Handled = true;
            }
        }
    }
}