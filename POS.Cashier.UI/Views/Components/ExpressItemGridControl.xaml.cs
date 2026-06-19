using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.Cashier.UI.Views.Components
{
    public partial class ExpressItemGridControl : UserControl
    {
        private readonly ExpressItemRepository _repository;

        // ==========================================
        // THE CUSTOM EVENT (For the main Sales window)
        // ==========================================
        // This acts as a microphone. When a button is clicked, this shouts the SKU Code out loud.
        public event EventHandler<string>? OnExpressItemClicked;

        // Data binding structure for the TabControl
        public ObservableCollection<ExpressTabGroup> TabGroups { get; set; } = new();

        // The command attached to every dynamic button
        public ICommand ItemClickedCommand { get; }

        public ExpressItemGridControl()
        {
            InitializeComponent();
            _repository = App.Services!.GetRequiredService<ExpressItemRepository>();

            // When a button is clicked, invoke the event, passing the SKU Code
            ItemClickedCommand = new RelayCommand<string>(skuCode =>
            {
                if (!string.IsNullOrWhiteSpace(skuCode))
                {
                    OnExpressItemClicked?.Invoke(this, skuCode);
                }
            });

            this.DataContext = this;
            this.Loaded += UserControl_Loaded;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadGridDataAsync();
        }

        public async Task LoadGridDataAsync()
        {
            try
            {
                var allLayouts = await _repository.GetAllLayoutsAsync();

                // Group the database layouts by TabCategory (e.g., "Drinks", "Bakery")
                var grouped = allLayouts
                    .GroupBy(x => x.TabCategory)
                    .Select(g => new ExpressTabGroup
                    {
                        CategoryName = g.Key,
                        Layouts = new ObservableCollection<ExpressItemLayout>(g)
                    })
                    .ToList();

                TabGroups.Clear();
                foreach (var group in grouped)
                {
                    TabGroups.Add(group);
                }

                // Bind the TabControl to our grouped data
                CategoryTabControl.ItemsSource = TabGroups;

                // Select the first tab automatically if it exists
                if (TabGroups.Any())
                {
                    CategoryTabControl.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load Express Items: {ex.Message}", "POS Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // A lightweight helper class to structure the Tab -> Buttons relationship
    public class ExpressTabGroup
    {
        public string CategoryName { get; set; } = string.Empty;
        public ObservableCollection<ExpressItemLayout> Layouts { get; set; } = new();
    }

    // Standard RelayCommand implementation for the UserControl
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T)parameter!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}