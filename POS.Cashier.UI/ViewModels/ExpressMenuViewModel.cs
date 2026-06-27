using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Cashier.UI.Messages;

namespace POS.Cashier.UI.ViewModels
{
    public partial class ExpressMenuViewModel : ObservableObject
    {
        private readonly ExpressItemRepository _repository;

        // Caches all buttons locally so switching tabs is instant (no database delay)
        private List<ExpressItemLayout> _allActiveButtons = new();

        [ObservableProperty]
        private ObservableCollection<string> _tabCategories = new();

        [ObservableProperty]
        private string _selectedTabCategory = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ExpressItemLayout> _currentTabButtons = new();

        [ObservableProperty]
        private bool _isLoading = true;

        public ExpressMenuViewModel(ExpressItemRepository repository)
        {
            _repository = repository;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;

            // 1. Fetch all active layouts designed by the Admin
            _allActiveButtons = await _repository.GetAllLayoutsAsync();

            // 2. Extract unique categories for the Sidebar Tabs
            var categories = _allActiveButtons
                .Select(b => b.TabCategory)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            foreach (var cat in categories)
            {
                TabCategories.Add(cat);
            }

            // 3. Auto-select the first tab
            if (TabCategories.Any())
            {
                SelectTab(TabCategories.First());
            }

            IsLoading = false;
        }

        [RelayCommand]
        private void SelectTab(string tabName)
        {
            if (string.IsNullOrWhiteSpace(tabName)) return;

            SelectedTabCategory = tabName;
            CurrentTabButtons.Clear();

            // Find all buttons that belong to this specific tab
            //var buttonsForTab = _allActiveButtons
            //    .Where(b => b.TabCategory == tabName)
            //    .OrderBy(b => b.GridRow)
            //    .ThenBy(b => b.GridColumn)
            //    .ToList();

            //foreach (var btn in buttonsForTab)
            //{
            //    CurrentTabButtons.Add(btn);
            //}
        }

        [RelayCommand]
        private void ButtonClicked(ExpressItemLayout layout)
        {
            if (layout == null || layout.ItemVariant == null) return;

            string skuToAdd = layout.ItemVariant.SkuCode;

            // THE MAGIC TRICK: 
            // Broadcast the SKU to the entire application. The SalesViewModel will hear this 
            // and instantly drop the item into the cart while this window stays open!
            WeakReferenceMessenger.Default.Send(new AddToCartMessage(skuToAdd));
        }
    }
}