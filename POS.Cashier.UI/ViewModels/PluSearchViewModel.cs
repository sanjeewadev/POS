//using System;
//using System.Collections.ObjectModel;
//using System.Threading.Tasks;
//using System.Windows;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using POS.Core.Models;
//using POS.Core.Repositories;

//namespace POS.Cashier.UI.ViewModels
//{
//    public partial class PluSearchViewModel : ObservableObject
//    {
//        private readonly ItemMasterRepository _itemRepository;

//        [ObservableProperty] private string _searchText = string.Empty;

//        // This holds the live results for the DataGrid
//        public ObservableCollection<ItemVariant> SearchResults { get; } = new();

//        public PluSearchViewModel(ItemMasterRepository itemRepository)
//        {
//            _itemRepository = itemRepository;
//        }

//        [RelayCommand]
//        public async Task SearchAsync()
//        {
//            try
//            {
//                SearchResults.Clear();

//                if (string.IsNullOrWhiteSpace(SearchText)) return;

//                var results = await _itemRepository.SearchActiveVariantsAsync(SearchText);

//                foreach (var item in results)
//                {
//                    SearchResults.Add(item);
//                }
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show($"Search failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
//            }
//        }

//        [RelayCommand]
//        public void ClearSearch()
//        {
//            SearchText = string.Empty;
//            SearchResults.Clear();
//        }
//    }
//}