using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class SupplierClaimsViewModel : ObservableObject
    {
        private readonly FreeItemClaimRepository _claimRepository;

        // --- Filters ---
        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _endDate = DateTime.Today;

        // --- Data ---
        public ObservableCollection<FreeItemClaimLog> ClaimedItems { get; } = new();

        // --- Summaries ---
        [ObservableProperty] private decimal _totalRecoverableAmount;
        [ObservableProperty] private int _totalItemsComped;

        [ObservableProperty] private bool _isLoading;

        public SupplierClaimsViewModel(FreeItemClaimRepository claimRepository)
        {
            _claimRepository = claimRepository;
        }

        [RelayCommand]
        public async Task LoadClaimsAsync()
        {
            IsLoading = true;
            try
            {
                ClaimedItems.Clear();

                // Fetch only the items flagged as IsRecoverable = true
                var claims = await _claimRepository.GetRecoverableClaimsAsync(StartDate, EndDate);

                foreach (var claim in claims)
                {
                    ClaimedItems.Add(claim);
                }

                // Calculate the bottom line
                TotalRecoverableAmount = claims.Sum(c => c.UnitCostAtTime * c.Quantity);
                TotalItemsComped = claims.Sum(c => c.Quantity);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading claims: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (ClaimedItems.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TODO: Wire up your preferred Excel/PDF export library (e.g., EPPlus or ClosedXML)
            MessageBox.Show($"Exporting {ClaimedItems.Count} records for supplier billing...", "Export Initiated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}