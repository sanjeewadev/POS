using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class GiftVoucherAdminViewModel : ViewModelBase
    {
        private readonly GiftVoucherRepository _repository;

        // --- Grid Data ---
        public ObservableCollection<GiftVoucherSummaryDto> Vouchers { get; set; } = new();

        // --- Filter ---
        [ObservableProperty]
        private string _selectedFilter = "All"; // "All", "Inactive", "Active", "Exhausted", "Expired", "Blocked"

        // --- Batch Generation Inputs ---
        [ObservableProperty]
        private int _generateCount = 50; // Default to 50 cards

        [ObservableProperty]
        private decimal _generateAmount = 5000m; // Default Rs. 5000 per card

        public GiftVoucherAdminViewModel(GiftVoucherRepository repository)
        {
            _repository = repository;
            _ = LoadVouchersAsync();
        }

        [RelayCommand]
        private async Task LoadVouchersAsync()
        {
            try
            {
                var data = await _repository.GetVoucherSummariesAsync(SelectedFilter);

                Vouchers.Clear();
                foreach (var item in data)
                {
                    Vouchers.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load vouchers: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        partial void OnSelectedFilterChanged(string value)
        {
            // Auto-reload the grid when the dropdown changes
            _ = LoadVouchersAsync();
        }

        [RelayCommand]
        private async Task GenerateBatchAsync()
        {
            if (GenerateCount <= 0 || GenerateCount > 5000)
            {
                MessageBox.Show("Please enter a valid count (1 - 5000).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (GenerateAmount <= 0)
            {
                MessageBox.Show("Please enter a valid monetary amount.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Are you sure you want to generate {GenerateCount} INACTIVE barcodes worth Rs. {GenerateAmount:N2} each?\n\n(Total Liability Value: Rs. {GenerateCount * GenerateAmount:N2})", "Confirm Generation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _repository.GenerateVoucherBatchAsync(GenerateCount, GenerateAmount);
                    MessageBox.Show("Batch generated successfully! Barcodes are ready for printing.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Reset to "Inactive" to show the newly created cards immediately
                    SelectedFilter = "Inactive";
                    _ = LoadVouchersAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate batch: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task BlockVoucherAsync(int voucherId)
        {
            var confirm = MessageBox.Show("Are you sure you want to permanently BLOCK this voucher?\nThis action cannot be undone and will freeze all remaining funds.", "Confirm Kill Switch", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _repository.BlockVoucherAsync(voucherId);
                    _ = LoadVouchersAsync(); // Refresh grid
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to block voucher: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportBarcodes()
        {
            // Note: In an enterprise system, this triggers a CSV/PDF export sent directly to the plastic card printing company.
            MessageBox.Show("Export module initializing...\n(Will generate CSV of selected barcodes for the printing press).", "Export to File", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}