using System;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Enums;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMenuVisible))]
        private object? _currentPage;

        public bool IsMenuVisible
        {
            get
            {
                if (CurrentPage != null && CurrentPage.GetType().Name == "LoginViewModel")
                {
                    return false;
                }
                return true;
            }
        }

        // We INJECT the factory into the MainViewModel so it can build other pages safely
        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LoadLoginScreen();
        }

        private void LoadLoginScreen()
        {
            // CRITICAL FIX: Do not use 'new LoginViewModel()'. 
            // Ask the DI Container to build it so it automatically injects the AuthService.
            var loginVM = _serviceProvider.GetRequiredService<LoginViewModel>();

            loginVM.LoginSuccessful += HandleSuccessfulLogin;
            CurrentPage = loginVM;
        }

        private void HandleSuccessfulLogin(UserRole role)
        {
            if (role == UserRole.Admin)
            {
                // Default landing page after a successful login
                NavigateToItemMaster();
            }
            else
            {
                // If a Cashier logs into the BackOffice by mistake, lock them out or redirect them.
                // For now, we will route them to Item Master, but you can restrict this later.
                NavigateToItemMaster();
            }
        }

        // ==========================================
        // TOP MENU NAVIGATION COMMANDS (Active)
        // ==========================================

        [RelayCommand]
        private void NavigateToCategory() => CurrentPage = _serviceProvider.GetRequiredService<CategoryViewModel>();

        [RelayCommand]
        private void NavigateToSubCategory() => CurrentPage = _serviceProvider.GetRequiredService<SubCategoryViewModel>();

        [RelayCommand]
        private void NavigateToItemProperty() => CurrentPage = _serviceProvider.GetRequiredService<ItemPropertyViewModel>();

        [RelayCommand]
        private void NavigateToSupplier() => CurrentPage = _serviceProvider.GetRequiredService<SupplierViewModel>();

        [RelayCommand]
        private void NavigateToUnitOfMeasure() => CurrentPage = _serviceProvider.GetRequiredService<UnitOfMeasureViewModel>();

        [RelayCommand]
        private void NavigateToItemMaster() => CurrentPage = _serviceProvider.GetRequiredService<ItemMasterViewModel>();

        // ==========================================
        // INVENTORY & PROCUREMENT COMMANDS (Active)
        // ==========================================

        [RelayCommand]
        private void NavigateToGoodsReceivedNote() => CurrentPage = _serviceProvider.GetRequiredService<GrnViewModel>();

        [RelayCommand]
        private void NavigateToPurchaseOrder() => CurrentPage = _serviceProvider.GetRequiredService<PurchaseOrderViewModel>();

        [RelayCommand]
        private void NavigateToStockBalance() => CurrentPage = _serviceProvider.GetRequiredService<StockBalanceViewModel>();

        [RelayCommand]
        private void NavigateToStockAdjustment() => CurrentPage = _serviceProvider.GetRequiredService<StockAdjustmentViewModel>();

        [RelayCommand]
        private void NavigateToSupplierReturn() => CurrentPage = _serviceProvider.GetRequiredService<SupplierReturnViewModel>();

        // ==========================================
        // SECURITY & ADMIN COMMANDS (Active)
        // ==========================================

        [RelayCommand]
        private void NavigateToUserManagement() => CurrentPage = _serviceProvider.GetRequiredService<UserManagementViewModel>();


        // ==========================================
        // LEFT SIDEBAR QUICK LAUNCH COMMANDS
        // ==========================================

        [RelayCommand]
        private void NavigateToInventorySetup() => NavigateToItemMaster();

        [RelayCommand]
        private void NavigateToInventoryOperations() => NavigateToGoodsReceivedNote();

        [RelayCommand]
        private void NavigateToPurchasing() => NavigateToPurchaseOrder();

        [RelayCommand]
        private void NavigateToSales() { } // Placeholder for future CRM/Sales dashboard

        [RelayCommand]
        private void NavigateToCrm() { } // Placeholder for future Customer dashboard

        [RelayCommand]
        private void NavigateToFinance() { }

        [RelayCommand]
        private void NavigateToReports() => NavigateToStockBalance();

        [RelayCommand]
        private void NavigateToAdmin() => NavigateToUserManagement();
    }
}