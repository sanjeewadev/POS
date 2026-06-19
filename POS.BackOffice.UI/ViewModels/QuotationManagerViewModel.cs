using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.DTOs;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class QuotationManagerViewModel : ViewModelBase
    {
        private readonly QuotationRepository _repository;

        // ==========================================
        // 1. UI STATE TOGGLES
        // ==========================================
        [ObservableProperty] private bool _isGridView = true;
        [ObservableProperty] private bool _isBuilderView = false;
        [ObservableProperty] private bool _isPrintPreviewOpen = false;

        // ==========================================
        // 2. GRID FILTERS & PAGINATION
        // ==========================================
        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "All";

        public ObservableCollection<string> AvailableStatuses { get; } = new()
        {
            "All", "Draft", "Sent", "Accepted", "Converted", "Expired"
        };

        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _pageSize = 50;
        [ObservableProperty] private int _totalPages = 1;

        public ObservableCollection<QuotationGridDto> PagedQuotations { get; set; } = new();

        // ==========================================
        // 3. BUILDER & PRINT STATE
        // ==========================================
        [ObservableProperty] private QuotationDetailDto _activeQuote = new();
        public ObservableCollection<QuotationLineDto> BuilderLines { get; set; } = new();

        [ObservableProperty] private string _itemSearchText = string.Empty;

        public QuotationManagerViewModel(QuotationRepository repository)
        {
            _repository = repository;
            _ = LoadGridAsync();
        }

        // ==============================================================================
        // ACTION: GRID SEARCH & PAGINATION
        // ==============================================================================
        [RelayCommand]
        private async Task LoadGridAsync()
        {
            try
            {
                var result = await _repository.GetPagedQuotationsAsync(
                    StartDate, EndDate, SearchText, SelectedStatus, CurrentPage, PageSize);

                PagedQuotations.Clear();
                foreach (var record in result.Records)
                {
                    PagedQuotations.Add(record);
                }

                TotalPages = (int)Math.Ceiling((double)result.TotalCount / PageSize);
                if (TotalPages == 0) TotalPages = 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading quotes: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand] private void Search() { CurrentPage = 1; _ = LoadGridAsync(); }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) { CurrentPage++; _ = LoadGridAsync(); } }
        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) { CurrentPage--; _ = LoadGridAsync(); } }

        // ==============================================================================
        // ACTION: UI NAVIGATION
        // ==============================================================================
        [RelayCommand]
        private void OpenBuilder()
        {
            ActiveQuote = new QuotationDetailDto
            {
                CashierName = "Admin", // TODO: Replace with your actual logged-in user session
                TerminalNo = "TERM-01",
                ValidUntil = DateTime.Today.AddDays(7)
            };
            BuilderLines.Clear();

            IsGridView = false;
            IsBuilderView = true;
        }

        [RelayCommand]
        private void CloseBuilder()
        {
            IsBuilderView = false;
            IsGridView = true;
            _ = LoadGridAsync(); // Refresh grid in case they saved a new quote
        }

        // ==============================================================================
        // ACTION: QUOTE DRAFTING & MATH
        // ==============================================================================
        [RelayCommand]
        private void AddPlaceholderItem()
        {
            // Note: In your final version, this will open an Item Search Dialog.
            // For now, we add a placeholder to test the math engine.
            BuilderLines.Add(new QuotationLineDto
            {
                ItemCode = "TEST-01",
                ItemDescription = "Wholesale Sample Product",
                Quantity = 1,
                UnitPrice = 1500m,
                CostPrice = 1000m,
                DiscountAmount = 0
            });
            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveItem(QuotationLineDto line)
        {
            if (line != null)
            {
                BuilderLines.Remove(line);
                RecalculateTotals();
            }
        }

        private void RecalculateTotals()
        {
            ActiveQuote.GrossTotal = BuilderLines.Sum(l => l.Quantity * l.UnitPrice);
            ActiveQuote.TotalDiscount = BuilderLines.Sum(l => l.DiscountAmount);
            ActiveQuote.NetTotal = ActiveQuote.GrossTotal - ActiveQuote.TotalDiscount;

            // Force UI Update for properties changed outside the setter
            OnPropertyChanged(nameof(ActiveQuote));
        }

        [RelayCommand]
        private async Task SaveQuoteAsync()
        {
            if (!BuilderLines.Any())
            {
                MessageBox.Show("Cannot save an empty quote. Add items first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ActiveQuote.Lines = BuilderLines.ToList();
                var generatedQuoteNo = await _repository.SaveQuotationAsync(ActiveQuote);

                MessageBox.Show($"Quote successfully generated!\nQuote No: {generatedQuoteNo}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseBuilder();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save quote: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==============================================================================
        // ACTION: ENTERPRISE CONVERSION (QUOTE -> SALE)
        // ==============================================================================
        [RelayCommand]
        private async Task ViewAndConvertAsync(QuotationGridDto selectedQuote)
        {
            if (selectedQuote == null) return;

            try
            {
                var fullQuote = await _repository.GetQuotationDetailsAsync(selectedQuote.QuotationId);
                if (fullQuote != null)
                {
                    ActiveQuote = fullQuote;
                    IsPrintPreviewOpen = true; // Opens the modal
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load quote details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ClosePrintPreview() => IsPrintPreviewOpen = false;

        [RelayCommand]
        private async Task ConvertToLiveSaleAsync()
        {
            if (ActiveQuote == null || ActiveQuote.Status == "Converted") return;

            var result = MessageBox.Show($"Are you sure you want to convert {ActiveQuote.QuoteNo} to a LIVE SALE?\n\nThis will immediately deduct physical stock and record revenue.", "Confirm Conversion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Note: '1' is a placeholder for the Active ShiftSessionId. 
                    // 'Cash' is the default payment method for this example.
                    await _repository.ConvertQuoteToSaleAsync(ActiveQuote.QuotationId, 1, "Cash");

                    MessageBox.Show("Success! Quote converted to live Sale Invoice.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    IsPrintPreviewOpen = false;
                    _ = LoadGridAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Conversion failed: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}