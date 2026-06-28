using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CreditNoteMasterWindow : Window
    {
        private ObservableCollection<MasterReturnItemDTO> _invoiceViewList = new ObservableCollection<MasterReturnItemDTO>();
        private ObservableCollection<MasterReturnItemDTO> _returnQueueList = new ObservableCollection<MasterReturnItemDTO>();

        public List<MasterReturnItemDTO> FinalCreditNoteLines => _returnQueueList.ToList();

        public CreditNoteMasterWindow()
        {
            InitializeComponent();
            dgInvoiceViewer.ItemsSource = _invoiceViewList;
            dgReturnQueue.ItemsSource = _returnQueueList;
            txtInvoiceNumber.Focus();
        }

        // Fetch Invoice Content Strategy
        private void FindInvoiceBtn_Click(object sender, RoutedEventArgs e)
        {
            string invNum = txtInvoiceNumber.Text.Trim();
            if (string.IsNullOrWhiteSpace(invNum)) return;

            var dbItems = new List<MasterReturnItemDTO>
            {
                new MasterReturnItemDTO { ItemCode = "ITM-991", ItemName = "Men's Slim Fit Shirt", PriceSold = 2500.00, QtySold = 2, ReturnQty = 1, ReturnRemarkType = "NORMAL" },
                new MasterReturnItemDTO { ItemCode = "ITM-102", ItemName = "Leather Belt Blue", PriceSold = 850.00, QtySold = 1, ReturnQty = 1, ReturnRemarkType = "NORMAL" }
            };

            _invoiceViewList.Clear();
            foreach (var item in dbItems) _invoiceViewList.Add(item);
            txtBarcodeScan.Clear();
        }

        // Add from Top Table to Bottom Queue Table
        private void AddFromInvoiceToReturn_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is MasterReturnItemDTO chosenItem)
            {
                if (_returnQueueList.Any(x => x.ItemCode == chosenItem.ItemCode)) return;

                var newItem = new MasterReturnItemDTO
                {
                    ItemCode = chosenItem.ItemCode,
                    ItemName = chosenItem.ItemName,
                    PriceSold = chosenItem.PriceSold,
                    QtySold = chosenItem.QtySold,
                    ReturnQty = 1,
                    ReturnRemarkType = "NORMAL" // Default value
                };

                newItem.PropertyChanged += ItemPropertiesChangedHandler;
                _returnQueueList.Add(newItem);
                UpdateSummaryGrandTotals();
            }
        }

        // Direct Item Barcode Scan (No Invoice Strategy)
        private void TxtBarcodeScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string scannedBarcode = txtBarcodeScan.Text.Trim();
                if (string.IsNullOrWhiteSpace(scannedBarcode)) return;

                var directItem = new MasterReturnItemDTO
                {
                    ItemCode = scannedBarcode,
                    ItemName = $"Direct Scanned Tag Item ({scannedBarcode})",
                    PriceSold = 1200.00,
                    QtySold = 999,
                    ReturnQty = 1,
                    ReturnRemarkType = "NORMAL"
                };

                if (!_returnQueueList.Any(x => x.ItemCode == directItem.ItemCode))
                {
                    directItem.PropertyChanged += ItemPropertiesChangedHandler;
                    _returnQueueList.Add(directItem);
                    UpdateSummaryGrandTotals();
                }

                txtBarcodeScan.Clear();
                txtInvoiceNumber.Clear();
                _invoiceViewList.Clear();
            }
        }

        private void ItemPropertiesChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MasterReturnItemDTO.ReturnQty) ||
                e.PropertyName == nameof(MasterReturnItemDTO.PriceSold) ||
                e.PropertyName == nameof(MasterReturnItemDTO.ReturnRemarkType))
            {
                UpdateSummaryGrandTotals();
            }
        }

        private void UpdateSummaryGrandTotals()
        {
            double combinedTotal = _returnQueueList.Sum(x => x.LineTotal);
            txtGrandTotalRefund.Text = combinedTotal.ToString("N2");
        }

        private void RemoveFromReturnQueue_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is MasterReturnItemDTO selectedQueueRow)
            {
                _returnQueueList.Remove(selectedQueueRow);
                UpdateSummaryGrandTotals();
            }
        }

        private void IssueBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_returnQueueList.Any())
            {
                MessageBox.Show("The execution queue is currently empty.", "Data Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Logic trigger verification loop
            foreach (var item in _returnQueueList)
            {
                if (item.ReturnQty > item.QtySold)
                {
                    MessageBox.Show($"Quantity Alert: Max sold for {item.ItemName} is {item.QtySold}.", "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            MessageBox.Show("Credit Note successfully processed and printed.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            this.DialogResult = true;
            this.Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class MasterReturnItemDTO : INotifyPropertyChanged
    {
        private double _priceSold;
        private double _returnQty;
        private string _returnRemarkType = "NORMAL"; // Defaulting to NORMAL

        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public double QtySold { get; set; }

        public string ReturnRemarkType
        {
            get => _returnRemarkType;
            set { _returnRemarkType = value; OnPropertyChanged(nameof(ReturnRemarkType)); }
        }

        public double PriceSold
        {
            get => _priceSold;
            set { _priceSold = value; OnPropertyChanged(nameof(PriceSold)); OnPropertyChanged(nameof(LineTotal)); }
        }

        public double ReturnQty
        {
            get => _returnQty;
            set { _returnQty = value; OnPropertyChanged(nameof(ReturnQty)); OnPropertyChanged(nameof(LineTotal)); }
        }

        public double LineTotal => PriceSold * ReturnQty;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}