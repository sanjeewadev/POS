using System;
using System.Windows;

namespace POS.Cashier.UI.Dialogs
{
    public partial class ReturnInvoiceDialog : Window
    {
        public ReturnInvoiceDialog()
        {
            InitializeComponent();
            RegisterInterfaceEvents();
        }

        private void RegisterInterfaceEvents()
        {
            if (BtnSearchInvoice != null) BtnSearchInvoice.Click += (s, e) => { /* Logic later */ };
            if (BtnAddDirectItem != null) BtnAddDirectItem.Click += (s, e) => { /* Logic later */ };
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            // When 'GENERATE EXCHANGE BILL' is clicked, close with true
            // Your logic layer will then trigger the receipt printer to output the barcode slip
            this.DialogResult = true;
            this.Close();
        }
    }
}