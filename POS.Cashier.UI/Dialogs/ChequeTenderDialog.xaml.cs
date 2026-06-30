using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ChequeTenderDialog : Window
    {
        private readonly ChequeTenderDialogViewModel _viewModel;

        public ChequeTenderDialog()
            : this(new ChequeTenderDialogViewModel())
        {
        }

        public ChequeTenderDialog(ChequeTenderDialogViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ActionCompleted += ViewModel_ActionCompleted;
        }

        public ChequeTenderDialogViewModel ViewModel => _viewModel;

        private void ViewModel_ActionCompleted(bool accepted)
        {
            DialogResult = accepted;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FocusChequeAmount();
        }

        // =========================================================
        // FOCUS ROUTING
        // =========================================================

        private void ChequeAmountTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _viewModel.SetAmountInputActive();
            SelectAll(ChequeAmountTextBox);
        }

        private void ChequeNoTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _viewModel.SetChequeNoInputActive();
            SelectAll(ChequeNoTextBox);
        }

        private void BankNameTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            BankNameTextBox.SelectAll();
        }

        private void BranchNameTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            BranchNameTextBox.SelectAll();
        }

        private void ChequeDatePicker_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // DatePicker can be changed with keyboard/mouse.
            // Numpad stays on previous numeric field.
        }

        private void ChequeAmountTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!ChequeAmountTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusChequeAmount();
            }
        }

        private void ChequeNoTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!ChequeNoTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusChequeNo();
            }
        }

        private void BankNameTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!BankNameTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                BankNameTextBox.Focus();
                BankNameTextBox.SelectAll();
            }
        }

        private void BranchNameTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!BranchNameTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                BranchNameTextBox.Focus();
                BranchNameTextBox.SelectAll();
            }
        }

        private void FocusChequeAmount()
        {
            _viewModel.SetAmountInputActive();
            ChequeAmountTextBox.Focus();
            SelectAll(ChequeAmountTextBox);
        }

        private void FocusChequeNo()
        {
            _viewModel.SetChequeNoInputActive();
            ChequeNoTextBox.Focus();
            SelectAll(ChequeNoTextBox);
        }

        private void FocusBankName()
        {
            BankNameTextBox.Focus();
            BankNameTextBox.SelectAll();
        }

        private void FocusBranchName()
        {
            BranchNameTextBox.Focus();
            BranchNameTextBox.SelectAll();
        }

        private void FocusChequeDate()
        {
            ChequeDatePicker.Focus();
        }

        private void SelectAll(TextBox textBox)
        {
            textBox.SelectAll();
            TenderNumpad.ResetFirstKey();
        }

        // =========================================================
        // ENTER / ESCAPE BEHAVIOR
        // =========================================================

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleEnterPressed();
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
            HandleEnterPressed();
        }

        private void HandleEnterPressed()
        {
            if (_viewModel.ActiveInputTarget == "Amount" &&
                ChequeAmountTextBox.IsKeyboardFocusWithin)
            {
                if (_viewModel.ValidateAmountBeforeMovingNext())
                    FocusChequeNo();

                return;
            }

            if (_viewModel.ActiveInputTarget == "ChequeNo" &&
                ChequeNoTextBox.IsKeyboardFocusWithin)
            {
                if (string.IsNullOrWhiteSpace(_viewModel.ChequeNo))
                {
                    _viewModel.ConfirmCommand.Execute(null);
                    FocusChequeNo();
                    return;
                }

                FocusBankName();
                return;
            }

            if (BankNameTextBox.IsKeyboardFocusWithin)
            {
                if (string.IsNullOrWhiteSpace(_viewModel.BankName))
                {
                    _viewModel.ConfirmCommand.Execute(null);
                    FocusBankName();
                    return;
                }

                FocusBranchName();
                return;
            }

            if (BranchNameTextBox.IsKeyboardFocusWithin)
            {
                FocusChequeDate();
                return;
            }

            _viewModel.ConfirmCommand.Execute(null);

            if (string.IsNullOrWhiteSpace(_viewModel.ChequeNo))
                FocusChequeNo();
            else if (string.IsNullOrWhiteSpace(_viewModel.BankName))
                FocusBankName();
        }

        // =========================================================
        // BUTTONS
        // =========================================================

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConfirmCommand.Execute(null);

            if (string.IsNullOrWhiteSpace(_viewModel.ChequeNo))
                FocusChequeNo();
            else if (string.IsNullOrWhiteSpace(_viewModel.BankName))
                FocusBankName();
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