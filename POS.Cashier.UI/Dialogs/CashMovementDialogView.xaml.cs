using System;
using System.Windows;
using System.Windows.Media;

namespace POS.Cashier.UI.Dialogs
{
    // 1. Create a custom list of options so the system knows the exact mode.
    public enum MovementType
    {
        PaidIn,
        PaidOut
    }

    public partial class CashMovementDialogView : Window
    {
        // We store the mode here so we know what to save to the database later.
        private MovementType _currentMode;

        // 2. We update the constructor to ask for the MovementType when it is created.
        public CashMovementDialogView(MovementType mode)
        {
            InitializeComponent();
            _currentMode = mode;

            // Run the UI setup as soon as the window opens
            SetupWindowUI();
        }

        // ==========================================
        // UI SETUP
        // ==========================================
        private void SetupWindowUI()
        {
            if (_currentMode == MovementType.PaidIn)
            {
                HeaderTxt.Text = "PAID IN";
                // Grab your custom green color directly from Colors.xaml
                HeaderBorder.Background = (SolidColorBrush)FindResource("CashBtnBrush");
            }
            else if (_currentMode == MovementType.PaidOut)
            {
                HeaderTxt.Text = "PAID OUT";
                // Grab your custom red color directly from Colors.xaml
                HeaderBorder.Background = (SolidColorBrush)FindResource("CancelBtnBrush");
            }
        }

        // ==========================================
        // ACTION BUTTONS
        // ==========================================
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            // Close the window without doing anything
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // We will add the database saving logic here later!

            // Tell the main screen it was successful and close
            this.DialogResult = true;
            this.Close();
        }
    }
}