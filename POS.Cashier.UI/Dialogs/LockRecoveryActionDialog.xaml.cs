using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public enum LockRecoveryAction
    {
        None = 0,
        UnlockAndContinue = 1,
        CloseApplication = 2
    }

    public partial class LockRecoveryActionDialog : Window
    {
        public LockRecoveryAction SelectedAction { get; private set; }

        public LockRecoveryActionDialog()
        {
            InitializeComponent();
        }

        private void UnlockAndContinue_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectedAction =
                LockRecoveryAction.UnlockAndContinue;

            DialogResult = true;
        }

        private void CloseApplication_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectedAction =
                LockRecoveryAction.CloseApplication;

            DialogResult = true;
        }

        private void Cancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            SelectedAction = LockRecoveryAction.None;
            DialogResult = false;
        }
    }
}
