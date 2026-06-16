using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class PriceDiscountDialog : Window
    {
        // Notice there is no ": Window" here on the constructor!
        public PriceDiscountDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }
    }
}