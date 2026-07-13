using System.Windows;

namespace POS.BackOffice.UI.Views.Dialogs
{
    public partial class GiftVoucherReasonDialog : Window
    {
        public string Reason { get; private set; } = string.Empty;

        public GiftVoucherReasonDialog(string actionTitle)
        {
            InitializeComponent();
            ActionText.Text = actionTitle;
            Loaded += (_, _) => ReasonBox.Focus();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            string reason = (ReasonBox.Text ?? string.Empty).Trim();
            if (reason.Length < 5)
            {
                MessageBox.Show("Enter a reason containing at least 5 characters.", "Gift Voucher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Reason = reason;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
