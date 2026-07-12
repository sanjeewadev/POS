using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class DrawerReasonDialog : Window
    {
        public string SelectedReason => ReasonCombo.SelectedItem?.ToString() ?? string.Empty;
        public string Note => (NoteBox.Text ?? string.Empty).Trim();

        public DrawerReasonDialog(string title, IEnumerable<string> reasons)
        {
            InitializeComponent();
            TitleText.Text = string.IsNullOrWhiteSpace(title) ? "CASH DRAWER" : title.Trim();
            foreach (string reason in reasons.Where(value => !string.IsNullOrWhiteSpace(value)))
                ReasonCombo.Items.Add(reason.Trim());
            if (ReasonCombo.Items.Count > 0)
                ReasonCombo.SelectedIndex = 0;
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;
            if (string.IsNullOrWhiteSpace(SelectedReason))
            {
                ErrorText.Text = "Select a reason.";
                return;
            }
            if (SelectedReason.Equals("Other", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Note))
            {
                ErrorText.Text = "A note is required for Other.";
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
