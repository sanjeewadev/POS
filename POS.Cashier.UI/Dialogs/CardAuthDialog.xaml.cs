using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CardAuthDialog : Window
    {
        public CardAuthDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }
    }
}