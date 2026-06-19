using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public class CustomerMasterViewModel : ViewModelBase
    {
        private readonly CustomerAdminRepository _repository;

        // ==========================================
        // TOP BAR CONTROLS
        // ==========================================
        public ObservableCollection<CustomerMaster> CustomersList { get; set; } = new();

        private string _selectedTypeFilter = "All Customers";
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set { _selectedTypeFilter = value; OnPropertyChanged(nameof(SelectedTypeFilter)); }
        }

        private string _searchKeyword = string.Empty;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set { _searchKeyword = value; OnPropertyChanged(nameof(SearchKeyword)); }
        }

        private CustomerMaster? _selectedCustomer;
        public CustomerMaster? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                _selectedCustomer = value;
                OnPropertyChanged(nameof(SelectedCustomer));
                OnPropertyChanged(nameof(IsCustomerSelected));
                if (value != null) PopulateFormFromSelected(value);
            }
        }

        public bool IsCustomerSelected => SelectedCustomer != null;

        // ==========================================
        // FORM INPUT PROPERTIES
        // ==========================================
        private int _currentCustomerId = 0;
        private string _inputCustomerCode = "[ AUTO ]";
        public string InputCustomerCode { get => _inputCustomerCode; set { _inputCustomerCode = value; OnPropertyChanged(nameof(InputCustomerCode)); } }

        private string _inputFullName = string.Empty;
        public string InputFullName { get => _inputFullName; set { _inputFullName = value; OnPropertyChanged(nameof(InputFullName)); } }

        private string _inputPhone = string.Empty;
        public string InputPhone { get => _inputPhone; set { _inputPhone = value; OnPropertyChanged(nameof(InputPhone)); } }

        private string _inputEmail = string.Empty;
        public string InputEmail { get => _inputEmail; set { _inputEmail = value; OnPropertyChanged(nameof(InputEmail)); } }

        private string _inputAddress = string.Empty;
        public string InputAddress { get => _inputAddress; set { _inputAddress = value; OnPropertyChanged(nameof(InputAddress)); } }

        private string _inputCompanyName = string.Empty;
        public string InputCompanyName { get => _inputCompanyName; set { _inputCompanyName = value; OnPropertyChanged(nameof(InputCompanyName)); } }

        private string _inputVatNumber = string.Empty;
        public string InputVatNumber { get => _inputVatNumber; set { _inputVatNumber = value; OnPropertyChanged(nameof(InputVatNumber)); } }

        private string _inputCustomerType = "Retail";
        public string InputCustomerType { get => _inputCustomerType; set { _inputCustomerType = value; OnPropertyChanged(nameof(InputCustomerType)); } }

        private bool _inputIsActive = true;
        public bool InputIsActive { get => _inputIsActive; set { _inputIsActive = value; OnPropertyChanged(nameof(InputIsActive)); } }

        private decimal _inputCreditLimit = 0m;
        public decimal InputCreditLimit { get => _inputCreditLimit; set { _inputCreditLimit = value; OnPropertyChanged(nameof(InputCreditLimit)); } }

        private int _inputCreditDays = 0;
        public int InputCreditDays { get => _inputCreditDays; set { _inputCreditDays = value; OnPropertyChanged(nameof(InputCreditDays)); } }

        private bool _inputIsCreditLocked = false;
        public bool InputIsCreditLocked { get => _inputIsCreditLocked; set { _inputIsCreditLocked = value; OnPropertyChanged(nameof(InputIsCreditLocked)); } }

        // ==========================================
        // COMMANDS
        // ==========================================
        public ICommand RefreshDataCommand { get; }
        public ICommand AddNewCustomerCommand { get; }
        public ICommand SaveCustomerCommand { get; }

        public CustomerMasterViewModel(CustomerAdminRepository repository)
        {
            _repository = repository;

            RefreshDataCommand = new RelayCommand(async (o) => await LoadDataAsync());
            AddNewCustomerCommand = new RelayCommand((o) => PrepareNewCustomer());
            SaveCustomerCommand = new RelayCommand(async (o) => await SaveCustomerAsync());

            _ = LoadDataAsync();
        }

        // ==========================================
        // LOGIC METHODS
        // ==========================================
        private async Task LoadDataAsync()
        {
            var data = await _repository.GetFilteredCustomersAsync(SelectedTypeFilter, SearchKeyword);
            CustomersList.Clear();
            foreach (var item in data) CustomersList.Add(item);
        }

        private void PopulateFormFromSelected(CustomerMaster c)
        {
            _currentCustomerId = c.Id;
            InputCustomerCode = c.CustomerCode;
            InputFullName = c.FullName;
            InputPhone = c.Phone;
            InputEmail = c.Email ?? string.Empty;
            InputAddress = c.Address ?? string.Empty;
            InputCompanyName = c.CompanyName ?? string.Empty;
            InputVatNumber = c.VatRegistrationNumber ?? string.Empty;
            InputCustomerType = c.CustomerType;
            InputIsActive = c.IsActive;
            InputCreditLimit = c.CreditLimit;
            InputCreditDays = c.CreditDays;
            InputIsCreditLocked = c.IsCreditLocked;
        }

        private void PrepareNewCustomer()
        {
            SelectedCustomer = null;
            _currentCustomerId = 0;
            InputCustomerCode = "[ AUTO ]";
            InputFullName = string.Empty;
            InputPhone = string.Empty;
            InputEmail = string.Empty;
            InputAddress = string.Empty;
            InputCompanyName = string.Empty;
            InputVatNumber = string.Empty;
            InputCustomerType = "Retail";
            InputIsActive = true;
            InputCreditLimit = 0m;
            InputCreditDays = 0;
            InputIsCreditLocked = false;

            // Force the UI to unlock the form grids
            OnPropertyChanged(nameof(IsCustomerSelected));
        }

        private async Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(InputFullName) || string.IsNullOrWhiteSpace(InputPhone))
            {
                MessageBox.Show("Full Name and Phone Number are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var customerToSave = new CustomerMaster
                {
                    Id = _currentCustomerId,
                    CustomerCode = _currentCustomerId == 0 ? string.Empty : InputCustomerCode,
                    FullName = InputFullName.Trim(),
                    Phone = InputPhone.Trim(),
                    Email = InputEmail.Trim(),
                    Address = InputAddress.Trim(),
                    CompanyName = InputCompanyName.Trim(),
                    VatRegistrationNumber = InputVatNumber.Trim(),
                    CustomerType = InputCustomerType,
                    IsActive = InputIsActive,
                    CreditLimit = InputCreditLimit,
                    CreditDays = InputCreditDays,
                    IsCreditLocked = InputIsCreditLocked
                };

                await _repository.SaveCustomerProfileAsync(customerToSave);

                MessageBox.Show("Customer profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}