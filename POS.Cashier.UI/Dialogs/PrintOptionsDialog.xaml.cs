using System.Windows;
using System.Windows.Input;

namespace POS.Cashier.UI.Dialogs
{
    public partial class PrintOptionsDialog : Window
    {
        public string SelectedPrintOption { get; private set; } = string.Empty;

        public PrintOptionsDialog()
        {
            InitializeComponent();
        }

        private void PrintLastBill_Click(object sender, RoutedEventArgs e)
        {
            SelectedPrintOption = "LastBill";
            DialogResult = true;
            Close();
        }

        private void PrintQuotation_Click(object sender, RoutedEventArgs e)
        {
            SelectedPrintOption = "Quotation";
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedPrintOption = string.Empty;
            DialogResult = false;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SelectedPrintOption = string.Empty;
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }
    }
}