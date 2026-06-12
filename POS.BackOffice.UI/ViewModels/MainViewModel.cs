using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
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

        // We INJECT the factory into the MainViewModel so it can build other pages
        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LoadLoginScreen();
        }

        private void LoadLoginScreen()
        {
            var loginVM = new LoginViewModel();
            loginVM.LoginSuccessful += HandleSuccessfulLogin;
            CurrentPage = loginVM;
        }

        private void HandleSuccessfulLogin(UserRole role)
        {
            if (role == UserRole.Admin)
            {
                NavigateToCategory();
            }
        }

        // ==========================================
        // TOP MENU NAVIGATION COMMANDS (Active)
        // ==========================================

        [RelayCommand]
        private void NavigateToCategory()
        {
            // Ask the factory to build the page safely
            CurrentPage = _serviceProvider.GetService(typeof(CategoryViewModel));
        }

        [RelayCommand]
        private void NavigateToSubCategory()
        {
            CurrentPage = _serviceProvider.GetService(typeof(SubCategoryViewModel));
        }

        [RelayCommand]
        private void NavigateToItemProperty()
        {
            CurrentPage = _serviceProvider.GetService(typeof(ItemPropertyViewModel));
        }

        [RelayCommand]
        private void NavigateToSupplier()
        {
            CurrentPage = _serviceProvider.GetService(typeof(SupplierViewModel));
        }

        // ==========================================
        // TOP MENU NAVIGATION COMMANDS (Pending)
        // ==========================================

        [RelayCommand]
        private void NavigateToItemMaster() {

            CurrentPage = _serviceProvider.GetService(typeof(ItemMasterViewModel));
        }

        [RelayCommand]
        private void NavigateToGoodsReceivedNote()
        {

            CurrentPage = _serviceProvider.GetService(typeof(GrnViewModel));
        }

        //[RelayCommand]
        //private void NavigatetoPurchaseOrder()
        //{ 
        
        //    CurrentPage = _serviceProvider.GetService(typeof(PerchasOrederViewModel))
        //}

        [RelayCommand]
        private void NavigateToUnitOfMeasure() { }

        // ==========================================
        // LEFT SIDEBAR QUICK LAUNCH COMMANDS
        // ==========================================

        [RelayCommand]
        private void NavigateToInventorySetup() => NavigateToCategory();

        [RelayCommand]
        private void NavigateToInventoryOperations() { }

        [RelayCommand]
        private void NavigateToPurchasing() { }

        [RelayCommand]
        private void NavigateToSales() { }

        [RelayCommand]
        private void NavigateToCrm() { }

        [RelayCommand]
        private void NavigateToFinance() { }

        [RelayCommand]
        private void NavigateToReports() { }

        [RelayCommand]
        private void NavigateToAdmin() { }
    }
}