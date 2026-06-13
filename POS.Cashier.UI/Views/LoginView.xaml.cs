using System.Windows;

namespace POS.Cashier.UI.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        // SECURITY ENHANCEMENT: 
        // The txtPassword_PasswordChanged event has been completely removed.
        // Memory vulnerabilities are eliminated by passing the secure PasswordBox 
        // directly to the ViewModel upon command execution.
    }
}