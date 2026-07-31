using System.Linq;
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

        // =========================================================
        // INPUT HELPERS: digits-only and decimal/number validation
        // =========================================================

        // Blocks non-digit characters for Last6 input
        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DigitsOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!text.All(char.IsDigit))
                e.CancelCommand();
        }

        // Allows digits and the current culture decimal separator, limits to a single separator and two fractional digits.
        private void Decimal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                e.Handled = true;
                return;
            }

            var decimalSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            // Reject any input text that contains non-digit and non-decimal-separator chars
            if (!e.Text.All(c => char.IsDigit(c) || c.ToString() == decimalSep))
            {
                e.Handled = true;
                return;
            }

            // Build proposed text after input (account for selection)
            string current = tb.Text ?? string.Empty;
            int selectionStart = tb.SelectionStart;
            int selectionLen = tb.SelectionLength;
            string proposed;
            if (selectionLen > 0)
                proposed = current.Remove(selectionStart, selectionLen).Insert(selectionStart, e.Text);
            else
                proposed = current.Insert(selectionStart, e.Text);

            // Only one decimal separator allowed
            if (proposed.Count(ch => ch.ToString() == decimalSep) > 1)
            {
                e.Handled = true;
                return;
            }

            // Limit fractional digits to 2
            int idx = proposed.IndexOf(decimalSep, System.StringComparison.Ordinal);
            if (idx >= 0)
            {
                int decimals = proposed.Length - idx - 1;
                if (decimals > 2)
                {
                    e.Handled = true;
                    return;
                }
            }

            e.Handled = false;
        }

        private void Decimal_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var pasteText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            var decimalSep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            // Reject if contains invalid chars
            if (!pasteText.All(c => char.IsDigit(c) || c.ToString() == decimalSep))
            {
                e.CancelCommand();
                return;
            }

            // Only one decimal separator allowed
            if (pasteText.Count(ch => ch.ToString() == decimalSep) > 1)
            {
                e.CancelCommand();
                return;
            }

            // If textbox already has a separator, ensure combined fractional length <= 2
            if (sender is TextBox tb)
            {
                string combined = tb.Text.Insert(tb.SelectionStart, pasteText);
                int idx = combined.IndexOf(decimalSep, System.StringComparison.Ordinal);
                if (idx >= 0)
                {
                    int decimals = combined.Length - idx - 1;
                    if (decimals > 2)
                    {
                        e.CancelCommand();
                        return;
                    }
                }
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.ActionCompleted -= ViewModel_ActionCompleted;
            base.OnClosed(e);
        }
    }
}