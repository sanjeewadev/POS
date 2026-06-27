//using System;
//using System.ComponentModel;
//using System.Windows;
//using System.Windows.Controls;
//using Microsoft.Extensions.DependencyInjection;
//using POS.Core.Interfaces;
//using POS.Core.Repositories;

//namespace POS.Cashier.UI.Dialogs
//{
//    public partial class LockScreenView : Window
//    {
//        private readonly string _currentCashierName;
//        private readonly TillRepository _authService;
//        private bool _isUnlocked = false;

//        public LockScreenView(string currentCashierName)
//        {
//            InitializeComponent();
//            _currentCashierName = currentCashierName;
//            CashierNameTxt.Text = $"Current User: {_currentCashierName}";

//            _authService = App.Services!.GetRequiredService<TillRepository>();
//        }

//        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
//        {
//            if (sender is Button btn && btn.Content != null)
//            {
//                PinBox.Password += btn.Content.ToString();
//            }
//        }

//        private void ClearBtn_Click(object sender, RoutedEventArgs e)
//        {
//            PinBox.Password = string.Empty;
//        }

//        private async void UnlockBtn_Click(object sender, RoutedEventArgs e)
//        {
//            if (string.IsNullOrWhiteSpace(PinBox.Password)) return;

//            string enteredPin = PinBox.Password;

//            // 1. Check the database for the real manager PIN
//            bool isAuthorized = await _authService.VerifyManagerPinAsync(enteredPin);

//            // 2. >>> THE DEVELOPER ESCAPE HATCH <<<
//            // Remove this before you sell the software to a real store!
//            if (enteredPin == "0000")
//            {
//                isAuthorized = true;
//            }

//            if (isAuthorized)
//            {
//                _isUnlocked = true; // Flips the security flag
//                this.Close();
//            }
//            else
//            {
//                MessageBox.Show("Incorrect PIN. Terminal remains locked.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
//                PinBox.Password = string.Empty;
//            }
//        }

//        // ==========================================
//        // ANTI-BYPASS SECURITY
//        // ==========================================
//        protected override void OnClosing(CancelEventArgs e)
//        {
//            // If a user tries to close the window via Alt+F4 or Task Manager injection
//            // while _isUnlocked is false, we cancel the close event.
//            if (!_isUnlocked)
//            {
//                e.Cancel = true;
//            }

//            base.OnClosing(e);
//        }
//    }
//}

using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class LockScreenView : Window
    {
        // Parameterless constructor fixes the "takes 1 arguments" error
        public LockScreenView()
        {
            InitializeComponent();
        }

        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            // Add pin typing logic here later
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            // Add pin clearing logic here later
        }

        private void UnlockBtn_Click(object sender, RoutedEventArgs e)
        {
            // Add unlock logic here later
        }
    }
}