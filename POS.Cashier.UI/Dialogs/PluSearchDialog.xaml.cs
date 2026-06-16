using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models;

namespace POS.Cashier.UI.Dialogs
{
    public partial class PluSearchDialog : Window
    {
        // This is the package we send back to the main checkout screen!
        public string SelectedBarcode { get; private set; } = string.Empty;

        //public PluSearchDialog()
        //{
        //    InitializeComponent();

        //    if (App.Services != null)
        //    {
        //        this.DataContext = App.Services.GetRequiredService<PluSearchViewModel>();
        //    }
        //}

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // Triggered when the cashier double-clicks a row in the DataGrid
        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Note: Ensure your DataGrid in PluSearchDialog.xaml has x:Name="ResultsGrid" 
            // and MouseDoubleClick="ResultsGrid_MouseDoubleClick"
            if (ResultsGrid.SelectedItem is ItemVariant selectedItem)
            {
                SelectedBarcode = selectedItem.Barcode;
                this.DialogResult = true;
                this.Close();
            }
        }

        // Triggered if you have a physical "Select/Add" button on the UI
        private void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ResultsGrid.SelectedItem is ItemVariant selectedItem)
            {
                SelectedBarcode = selectedItem.Barcode;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select an item from the list first.", "Select Item", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}