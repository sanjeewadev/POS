using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class BatchSelectionDialog : Window
    {
        public BatchSelectionDialog()
        {
            InitializeComponent();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // Validation will be handled in Phase 3
            this.DialogResult = true;
            this.Close();
        }
    }
}