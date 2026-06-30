using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CashTenderDialog : Window
    {
        private readonly CashTenderDialogViewModel _viewModel;

        public CashTenderDialog()
            : this(new CashTenderDialogViewModel())
        {
        }

        public CashTenderDialog(CashTenderDialogViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ActionCompleted += ViewModel_ActionCompleted;
        }

        public CashTenderDialogViewModel ViewModel => _viewModel;

        private void ViewModel_ActionCompleted(bool accepted)
        {
            DialogResult = accepted;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FocusTenderedAmount();
        }

        private void TenderedAmountTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            SelectAllTenderedAmount();
        }

        private void TenderedAmountTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!TenderedAmountTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusTenderedAmount();
            }
        }

        private void FocusTenderedAmount()
        {
            TenderedAmountTextBox.Focus();
            SelectAllTenderedAmount();
        }

        private void SelectAllTenderedAmount()
        {
            TenderedAmountTextBox.SelectAll();
            TenderNumpad.ResetFirstKey();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                _viewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                _viewModel.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void TenderNumpad_EnterPressed(object? sender, System.EventArgs e)
        {
            _viewModel.ConfirmCommand.Execute(null);
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConfirmCommand.Execute(null);
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelCommand.Execute(null);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.ActionCompleted -= ViewModel_ActionCompleted;
            base.OnClosed(e);
        }
    }
}