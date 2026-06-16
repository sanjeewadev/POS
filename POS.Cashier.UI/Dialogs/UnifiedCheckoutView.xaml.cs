using System.Windows;
using System.Windows.Controls;
using POS.Cashier.UI.ViewModels;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class UnifiedCheckoutView : Window
    {
        public UnifiedCheckoutView()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                if (this.DataContext is CheckoutViewModel vm)
                {
                    vm.RequestClose = (success) => {
                        this.DialogResult = success;
                        this.Close();
                    };
                }
            };
        }



        private void Numpad_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                if (this.DataContext is CheckoutViewModel vm)
                {
                    string value = btn.Content.ToString() ?? "";

                    // Security constraint: Prevent multiple decimals
                    if (value == "." && vm.TenderedInput.Contains(".")) return;

                    // Smart Input Logic: Override initial zero
                    if (vm.TenderedInput == "0" && value != ".")
                    {
                        vm.TenderedInput = value;
                    }
                    else
                    {
                        // Hard cap input length to prevent accidental million-rupee typos
                        if (vm.TenderedInput.Length < 12)
                        {
                            vm.TenderedInput += value;
                        }
                    }
                }
            }
        }

        private void NumpadClear_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is CheckoutViewModel vm)
            {
                // Instantly reset the input buffer
                vm.TenderedInput = "0";
            }
        }
    }
}