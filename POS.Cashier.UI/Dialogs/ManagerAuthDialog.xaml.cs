using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Interfaces;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class ManagerAuthDialog : Window
    {
        private readonly ICashMovementService _authService;

        public ManagerAuthDialog()
        {
            InitializeComponent();
            _authService = App.Services!.GetRequiredService<ICashMovementService>();
        }

        private void NumpadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                PinBox.Password += btn.Content.ToString();
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            PinBox.Password = string.Empty;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PinBox.Password)) return;

            string enteredPin = PinBox.Password;

            // Lock the UI to prevent spamming
            PinBox.IsEnabled = false;

            try
            {
                bool isAuthorized = await _authService.VerifyManagerPinAsync(enteredPin);

                if (isAuthorized)
                {
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Invalid Manager PIN.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    PinBox.Password = string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verification error: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PinBox.IsEnabled = true;
            }
        }
    }
}