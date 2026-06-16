using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CashTenderDialog : Window
    {
        public CashTenderDialog()
        {
            InitializeComponent();

            // Temporary DataContext so the window doesn't crash before you build the ViewModel
            this.DataContext = this;
        }
    }
}