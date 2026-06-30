using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CardTenderDialog : Window
    {
        private readonly CardTenderDialogViewModel _viewModel;

        public CardTenderDialog()
            : this(new CardTenderDialogViewModel())
        {
        }

        public CardTenderDialog(CardTenderDialogViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.ActionCompleted += ViewModel_ActionCompleted;
        }

        public CardTenderDialogViewModel ViewModel => _viewModel;

        private void ViewModel_ActionCompleted(bool accepted)
        {
            DialogResult = accepted;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FocusCardAmount();
        }

        // =========================================================
        // FOCUS ROUTING
        // =========================================================

        private void CardAmountTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _viewModel.SetAmountInputActive();
            SelectAll(CardAmountTextBox);
        }

        private void LastSixDigitsTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _viewModel.SetLastSixInputActive();
            SelectAll(LastSixDigitsTextBox);
        }

        private void ReferenceTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            _viewModel.SetReferenceInputActive();
            SelectAll(ReferenceTextBox);
        }

        private void CardAmountTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!CardAmountTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusCardAmount();
            }
        }

        private void LastSixDigitsTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!LastSixDigitsTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusLastSixDigits();
            }
        }

        private void ReferenceTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!ReferenceTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusReference();
            }
        }

        private void FocusCardAmount()
        {
            _viewModel.SetAmountInputActive();
            CardAmountTextBox.Focus();
            SelectAll(CardAmountTextBox);
        }

        private void FocusLastSixDigits()
        {
            _viewModel.SetLastSixInputActive();
            LastSixDigitsTextBox.Focus();
            SelectAll(LastSixDigitsTextBox);
        }

        private void FocusReference()
        {
            _viewModel.SetReferenceInputActive();
            ReferenceTextBox.Focus();
            SelectAll(ReferenceTextBox);
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
            if (_viewModel.ActiveInputTarget == "Amount")
            {
                if (_viewModel.ValidateAmountBeforeMovingNext())
                    FocusLastSixDigits();

                return;
            }

            _viewModel.ConfirmCommand.Execute(null);

            if (_viewModel.RequireLastSixDigits && _viewModel.LastSixDigits.Length != 6)
                FocusLastSixDigits();
        }

        // =========================================================
        // BUTTONS
        // =========================================================

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConfirmCommand.Execute(null);

            if (_viewModel.RequireLastSixDigits && _viewModel.LastSixDigits.Length != 6)
                FocusLastSixDigits();
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