using System.Windows;

namespace POS.BackOffice.UI.Services
{
    public class MessageBoxService : IMessageBoxService
    {
        public void ShowInformation(string message, string title = "Information")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title = "Warning")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title = "Error")
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        public bool ShowConfirmation(
            string message,
            string title = "Confirm",
            MessageBoxImage icon = MessageBoxImage.Question)
        {
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                icon);

            return result == MessageBoxResult.Yes;
        }
    }
}