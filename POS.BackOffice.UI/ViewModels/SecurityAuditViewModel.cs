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
    public partial class SecurityAuditViewModel : ViewModelBase
    {
        private readonly SecurityAuditRepository _repository;

        // ==========================================
        // 1. FILTER PROPERTIES
        // ==========================================
        [ObservableProperty] private DateTime _startDate = DateTime.Today.AddDays(-7); // Default to a 7-day security review
        [ObservableProperty] private DateTime _endDate = DateTime.Today;

        // ==========================================
        // 2. MACRO KPI CARDS (Security Summary)
        // ==========================================
        [ObservableProperty] private int _totalVoidCount;
        [ObservableProperty] private decimal _totalVoidAmount;
        [ObservableProperty] private int _totalReturnCount;
        [ObservableProperty] private decimal _totalReturnAmount;
        [ObservableProperty] private int _suspendedCartCount;
        [ObservableProperty] private int _highRiskCashierCount;

        // ==========================================
        // 3. AUDIT COLLECTIONS (For DataGrids)
        // ==========================================
        public ObservableCollection<CashierFraudRiskDto> CashierRiskProfiles { get; set; } = new();
        public ObservableCollection<ReturnAuditRecordDto> ReturnRecords { get; set; } = new();
        public ObservableCollection<VoidAuditRecordDto> VoidRecords { get; set; } = new();

        public SecurityAuditViewModel(SecurityAuditRepository repository)
        {
            _repository = repository;
            _ = LoadSecurityAuditAsync();
        }

        // ==========================================
        // 4. THE ENGINE COMMANDS
        // ==========================================
        [RelayCommand]
        private async Task LoadSecurityAuditAsync()
        {
            if (StartDate > EndDate)
            {
                MessageBox.Show("Start Date cannot be later than End Date.", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Load Macro KPIs
                var summary = await _repository.GetSecuritySummaryAsync(StartDate, EndDate);
                TotalVoidCount = summary.TotalVoidCount;
                TotalVoidAmount = summary.TotalVoidAmount;
                TotalReturnCount = summary.TotalReturnCount;
                TotalReturnAmount = summary.TotalReturnAmount;
                SuspendedCartCount = summary.SuspendedCartCount;
                HighRiskCashierCount = summary.HighRiskCashierCount;

                // 2. Load Cashier Risk Profiles
                var profiles = await _repository.GetCashierRiskProfilesAsync(StartDate, EndDate);
                CashierRiskProfiles.Clear();
                foreach (var profile in profiles)
                {
                    CashierRiskProfiles.Add(profile);
                }

                // 3. Load Raw Returns
                var returns = await _repository.GetReturnRecordsAsync(StartDate, EndDate);
                ReturnRecords.Clear();
                foreach (var record in returns)
                {
                    ReturnRecords.Add(record);
                }

                // 4. Load Raw Voids
                var voids = await _repository.GetVoidRecordsAsync(StartDate, EndDate);
                VoidRecords.Clear();
                foreach (var record in voids)
                {
                    VoidRecords.Add(record);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading security audit: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ApplyFilters()
        {
            _ = LoadSecurityAuditAsync();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            StartDate = DateTime.Today.AddDays(-7);
            EndDate = DateTime.Today;
            _ = LoadSecurityAuditAsync();
        }
    }
}