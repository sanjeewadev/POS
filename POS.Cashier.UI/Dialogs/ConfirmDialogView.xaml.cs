using System;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ConfirmDialogView : Window
    {
        // When we open this window, we MUST give it a title and a message.
        public ConfirmDialogView(string customTitle, string customMessage)
        {
            InitializeComponent();

            // This takes the text we passed in and puts it on the screen!
            TitleTxt.Text = customTitle;
            MessageTxt.Text = customMessage;
        }

        // ==========================================
        // ACTION BUTTONS
        // ==========================================

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            // The cashier clicked "NO, GO BACK"
            // We tell the main screen the answer was "False"
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // The cashier clicked "YES, I'M SURE"
            // We tell the main screen the answer was "True"
            this.DialogResult = true;
            this.Close();
        }
    }
}