using System.Windows;

namespace POS.BackOffice.UI.Services
{
    public interface IMessageBoxService
    {
        void ShowInformation(string message, string title = "Information");

        void ShowWarning(string message, string title = "Warning");

        void ShowError(string message, string title = "Error");

        bool ShowConfirmation(
            string message,
            string title = "Confirm",
            MessageBoxImage icon = MessageBoxImage.Question);
    }
}