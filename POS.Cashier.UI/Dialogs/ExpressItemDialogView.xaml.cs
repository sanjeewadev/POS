using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.Cashier.UI.Views.Dialogs
{
    public partial class ExpressItemDialogView : Window
    {
        private readonly ExpressItemRepository _repository;

        // This is the property your main SalesView will read after the popup closes
        public string SelectedSkuCode { get; private set; } = string.Empty;

        public ObservableCollection<ExpressTabGroup> TabGroups { get; set; } = new();
        public ICommand ItemClickedCommand { get; }

        public ExpressItemDialogView()
        {
            InitializeComponent();
            _repository = App.Services!.GetRequiredService<ExpressItemRepository>();

            // When a cashier taps a colored button
            ItemClickedCommand = new RelayCommand<string>(skuCode =>
            {
                if (!string.IsNullOrWhiteSpace(skuCode))
                {
                    SelectedSkuCode = skuCode;
                    this.DialogResult = true; // Signal success to the main window
                    this.Close(); // Instantly vanish the popup
                }
            });

            this.DataContext = this;
            this.Loaded += Window_Loaded;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadGridDataAsync();
        }

        public async Task LoadGridDataAsync()
        {
            try
            {
                var allLayouts = await _repository.GetAllLayoutsAsync();

                var grouped = allLayouts
                    .GroupBy(x => x.TabCategory)
                    .Select(g => new ExpressTabGroup
                    {
                        CategoryName = g.Key,
                        Layouts = new ObservableCollection<ExpressItemLayout>(g)
                    })
                    .ToList();

                TabGroups.Clear();
                foreach (var group in grouped) TabGroups.Add(group);

                CategoryTabControl.ItemsSource = TabGroups;

                if (TabGroups.Any()) CategoryTabControl.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Express Items: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class ExpressTabGroup
    {
        public string CategoryName { get; set; } = string.Empty;
        public ObservableCollection<ExpressItemLayout> Layouts { get; set; } = new();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T)parameter!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}