using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CashTenderDialog : Window
    {
        private readonly CashTenderDialogViewModel _viewModel;
        private bool _completionInProgress;

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
            if (!IsVisible)
                return;

            // Setting DialogResult closes a modal WPF dialog. Do not call Close
            // again because every completion route must finish exactly once.
            DialogResult = accepted;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FocusTenderedAmount();
        }

        private void TenderedAmountTextBox_GotKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            SelectAllTenderedAmount();
        }

        private void TenderedAmountTextBox_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (!TenderedAmountTextBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                FocusTenderedAmount();
            }
        }

        private void TenderedAmountTextBox_PreviewTextInput(
            object sender,
            TextCompositionEventArgs e)
        {
            string candidate = BuildCandidateText(
                TenderedAmountTextBox,
                e.Text);

            e.Handled = !IsValidMoneyCandidate(candidate);
        }

        private void TenderedAmountTextBox_Pasting(
            object sender,
            DataObjectPastingEventArgs e)
        {
            if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                e.CancelCommand();
                return;
            }

            string pastedText =
                e.SourceDataObject.GetData(DataFormats.UnicodeText, true)
                    as string ?? string.Empty;

            string candidate = BuildCandidateText(
                TenderedAmountTextBox,
                pastedText.Trim());

            if (!IsValidMoneyCandidate(candidate))
                e.CancelCommand();
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
                ExecuteConfirm();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                ExecuteCancel();
                e.Handled = true;
            }
        }

        private void TenderNumpad_EnterPressed(object? sender, EventArgs e)
        {
            ExecuteConfirm();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            ExecuteConfirm();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCancel();
        }

        private void ExecuteConfirm()
        {
            if (_completionInProgress)
                return;

            if (!_viewModel.CanConfirm)
            {
                _viewModel.ConfirmCommand.Execute(null);
                FocusTenderedAmount();
                return;
            }

            _completionInProgress = true;

            try
            {
                _viewModel.ConfirmCommand.Execute(null);
            }
            finally
            {
                // A validation failure keeps the dialog visible and usable.
                // A successful command closes it through ActionCompleted.
                if (IsVisible)
                    _completionInProgress = false;
            }
        }

        private void ExecuteCancel()
        {
            if (_completionInProgress)
                return;

            _completionInProgress = true;
            _viewModel.CancelCommand.Execute(null);
        }

        private static string BuildCandidateText(
            TextBox textBox,
            string insertedText)
        {
            string currentText = textBox.Text ?? string.Empty;
            int selectionStart = Math.Clamp(
                textBox.SelectionStart,
                0,
                currentText.Length);
            int selectionLength = Math.Clamp(
                textBox.SelectionLength,
                0,
                currentText.Length - selectionStart);

            return currentText
                .Remove(selectionStart, selectionLength)
                .Insert(selectionStart, insertedText);
        }

        private static bool IsValidMoneyCandidate(string value)
        {
            if (value.Length > 12)
                return false;

            int decimalPointCount = 0;

            foreach (char character in value)
            {
                if (char.IsDigit(character) || character == ',')
                    continue;

                if (character == '.')
                {
                    decimalPointCount++;

                    if (decimalPointCount <= 1)
                        continue;
                }

                return false;
            }

            return true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.ActionCompleted -= ViewModel_ActionCompleted;
            base.OnClosed(e);
        }
    }
}
