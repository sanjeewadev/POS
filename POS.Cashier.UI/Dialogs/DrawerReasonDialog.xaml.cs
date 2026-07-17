using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class DrawerReasonDialog : Window
    {
        public string SelectedReason => ReasonCombo.SelectedItem?.ToString() ?? string.Empty;
        public string Note => (NoteBox.Text ?? string.Empty).Trim();

        public DrawerReasonDialog(string title, IEnumerable<string> reasons)
        {
            InitializeComponent();
            TitleText.Text = string.IsNullOrWhiteSpace(title) ? "CASH DRAWER" : title.Trim().ToUpperInvariant();

            foreach (string reason in reasons.Where(value => !string.IsNullOrWhiteSpace(value)))
                ReasonCombo.Items.Add(reason.Trim());

            if (ReasonCombo.Items.Count > 0)
                ReasonCombo.SelectedIndex = 0;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReasonCombo.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            e.Handled = true;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(SelectedReason))
            {
                ErrorText.Text = "Select a reason.";
                ReasonCombo.Focus();
                return;
            }

            if (SelectedReason.Equals("Other", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(Note))
            {
                ErrorText.Text = "A note is required for Other.";
                NoteBox.Focus();
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
