using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class CashDenominationDialog : Window
    {
        public ObservableCollection<DenominationRow> Rows { get; set; }
        public decimal FinalTotal { get; private set; }

        public CashDenominationDialog()
        {
            InitializeComponent();

            // Standard Sri Lankan Denominations
            Rows = new ObservableCollection<DenominationRow>
            {
                new DenominationRow(5000), new DenominationRow(1000),
                new DenominationRow(500), new DenominationRow(100),
                new DenominationRow(50), new DenominationRow(20),
                new DenominationRow(10), new DenominationRow(5)
            };

            // Listen for changes so we can update the Grand Total live
            foreach (var row in Rows)
            {
                row.PropertyChanged += Row_PropertyChanged;
            }

            DenominationGrid.ItemsSource = Rows;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DenominationRow.LineTotal))
            {
                FinalTotal = Rows.Sum(r => r.LineTotal);
                GrandTotalText.Text = $"Rs. {FinalTotal:N2}";
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    // Lightweight internal model to handle the math automatically
    public class DenominationRow : INotifyPropertyChanged
    {
        public decimal DenominationValue { get; }
        private int _quantity;

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineTotal)));
            }
        }

        public decimal LineTotal => DenominationValue * Quantity;

        public DenominationRow(decimal value) { DenominationValue = value; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}