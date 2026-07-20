using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace POS.Cashier.UI.Dialogs
{
    public partial class CashierConfirmationDialog : Window
    {
        public CashierConfirmationDialog(
            string title,
            string heading,
            string message,
            string detail,
            string confirmText,
            string cancelText,
            bool isDangerAction = true)
        {
            InitializeComponent();

            Title = string.IsNullOrWhiteSpace(title)
                ? "Confirm Cashier Action"
                : title.Trim();

            HeadingTextBlock.Text = string.IsNullOrWhiteSpace(heading)
                ? "CONFIRM ACTION"
                : heading.Trim().ToUpperInvariant();

            MessageTextBlock.Text = message?.Trim() ?? string.Empty;
            ConfirmButton.Content = string.IsNullOrWhiteSpace(confirmText)
                ? "CONFIRM"
                : confirmText.Trim().ToUpperInvariant();
            CancelButton.Content = string.IsNullOrWhiteSpace(cancelText)
                ? "BACK"
                : cancelText.Trim().ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(detail))
            {
                DetailTextBlock.Text = detail.Trim();
                DetailBorder.Visibility = Visibility.Visible;
            }

            if (!isDangerAction)
            {
                HeaderIconBorder.Background = new SolidColorBrush(
                    Color.FromRgb(37, 99, 235));
                ConfirmButton.Style = (Style)FindResource(
                    "CashierTransactionPrimaryButton");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConfirmButton.Focus();
            Keyboard.Focus(ConfirmButton);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                DialogResult = !ReferenceEquals(
                    Keyboard.FocusedElement,
                    CancelButton);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left || e.Key == Key.Right)
            {
                Button target = ReferenceEquals(
                    Keyboard.FocusedElement,
                    ConfirmButton)
                    ? CancelButton
                    : ConfirmButton;

                target.Focus();
                Keyboard.Focus(target);
                e.Handled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
