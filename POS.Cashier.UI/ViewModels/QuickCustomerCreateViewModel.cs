using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;

namespace POS.Cashier.UI.ViewModels
{
    public partial class QuickCustomerCreateViewModel : ObservableObject
    {
        private readonly CustomerRepository _customerRepository;

        public ObservableCollection<string> CustomerTypes { get; } = new()
        {
            "Retail",
            "Wholesale"
        };

        // =========================================================
        // INPUTS
        // =========================================================

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWholesale))]
        [NotifyPropertyChangedFor(nameof(IsRetail))]
        [NotifyPropertyChangedFor(nameof(IdentityLabel))]
        [NotifyPropertyChangedFor(nameof(CustomerTypeHelpText))]
        private string _inputCustomerType = "Retail";

        [ObservableProperty]
        private string _inputFullName = string.Empty;

        [ObservableProperty]
        private string _inputPhone = string.Empty;

        [ObservableProperty]
        private string _inputEmail = string.Empty;

        [ObservableProperty]
        private DateTime? _inputBirthday;

        [ObservableProperty]
        private string _inputAddress = string.Empty;

        [ObservableProperty]
        private string _inputNicNumber = string.Empty;

        [ObservableProperty]
        private string _inputCompanyName = string.Empty;

        [ObservableProperty]
        private string _inputBusinessRegistrationNumber = string.Empty;

        [ObservableProperty]
        private string _inputVatRegistrationNumber = string.Empty;

        // =========================================================
        // RESULT / STATUS
        // =========================================================

        [ObservableProperty]
        private CustomerSearchDto? _createdCustomer;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusText = "Enter basic customer details.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        public bool IsRetail =>
            InputCustomerType.Equals("Retail", StringComparison.OrdinalIgnoreCase);

        public bool IsWholesale =>
            InputCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase);

        public string IdentityLabel =>
            IsWholesale ? "BR No:" : "NIC No:";

        public string CustomerTypeHelpText
        {
            get
            {
                if (IsWholesale)
                    return "Wholesale customer will be created without credit or discount approval.";

                return "Retail customer will be created without loyalty approval.";
            }
        }

        public event Action<bool>? ActionCompleted;

        public QuickCustomerCreateViewModel(CustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy)
                return;

            if (!ValidateInputs())
                return;

            try
            {
                IsBusy = true;
                StatusText = "Saving customer...";
                StatusColorHex = "#3B82F6";

                string safeCustomerType = NormalizeCustomerType(InputCustomerType);

                var customer = new CustomerMaster
                {
                    FullName = NormalizeText(InputFullName),
                    Phone = NormalizePhone(InputPhone),
                    Email = NormalizeText(InputEmail),
                    Birthday = InputBirthday,
                    Address = NormalizeText(InputAddress),

                    CustomerType = safeCustomerType,

                    NicNumber = safeCustomerType.Equals("Retail", StringComparison.OrdinalIgnoreCase)
                        ? NormalizeText(InputNicNumber)
                        : string.Empty,

                    CompanyName = safeCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase)
                        ? NormalizeText(InputCompanyName)
                        : string.Empty,

                    BusinessRegistrationNumber = safeCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase)
                        ? NormalizeText(InputBusinessRegistrationNumber)
                        : string.Empty,

                    VatRegistrationNumber = safeCustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase)
                        ? NormalizeText(InputVatRegistrationNumber)
                        : string.Empty,

                    // Cashier-created customers are basic only.
                    // Admin/BackOffice must approve loyalty, discount and credit later.
                    IsDiscountEligible = false,
                    IsCreditEnabled = false,
                    CreditStatus = "None",
                    CreditLimit = 0m,
                    CreditDays = 0,
                    CurrentBalance = 0m,
                    IsCreditLocked = false,

                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = "Cashier"
                };

                var savedCustomer = await _customerRepository.SaveCustomerProfileAsync(customer);

                CreatedCustomer = BuildCustomerSearchDto(savedCustomer);

                StatusText = $"Customer saved: {CreatedCustomer.CustomerCode} - {CreatedCustomer.DisplayName}";
                StatusColorHex = "#10B981";

                // This only means the save was successful.
                // SalesView.xaml.cs should NOT attach this customer automatically.
                ActionCompleted?.Invoke(true);
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
                StatusColorHex = "#EF4444";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            InputCustomerType = "Retail";
            InputFullName = string.Empty;
            InputPhone = string.Empty;
            InputEmail = string.Empty;
            InputBirthday = null;
            InputAddress = string.Empty;
            InputNicNumber = string.Empty;
            InputCompanyName = string.Empty;
            InputBusinessRegistrationNumber = string.Empty;
            InputVatRegistrationNumber = string.Empty;

            CreatedCustomer = null;
            StatusText = "Enter basic customer details.";
            StatusColorHex = "#003366";
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private bool ValidateInputs()
        {
            string fullName = NormalizeText(InputFullName);
            string phone = NormalizePhone(InputPhone);
            string customerType = NormalizeCustomerType(InputCustomerType);

            if (string.IsNullOrWhiteSpace(fullName))
            {
                StatusText = "Customer name is required.";
                StatusColorHex = "#EF4444";
                return false;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                StatusText = "Phone number is required.";
                StatusColorHex = "#EF4444";
                return false;
            }

            if (!customerType.Equals("Retail", StringComparison.OrdinalIgnoreCase) &&
                !customerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
            {
                StatusText = "Customer type must be Retail or Wholesale.";
                StatusColorHex = "#EF4444";
                return false;
            }

            if (customerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(NormalizeText(InputCompanyName)))
            {
                StatusText = "Company name is required for wholesale customer.";
                StatusColorHex = "#EF4444";
                return false;
            }

            return true;
        }

        // =========================================================
        // DTO BUILDER
        // =========================================================

        private static CustomerSearchDto BuildCustomerSearchDto(CustomerMaster customer)
        {
            return new CustomerSearchDto
            {
                Id = customer.Id,
                CustomerCode = customer.CustomerCode,
                FullName = customer.FullName,
                CompanyName = customer.CompanyName,
                Phone = customer.Phone,
                CustomerType = customer.CustomerType,
                Birthday = customer.Birthday,
                NicNumber = customer.NicNumber,
                BusinessRegistrationNumber = customer.BusinessRegistrationNumber,
                VatRegistrationNumber = customer.VatRegistrationNumber,
                IsDiscountEligible = customer.IsDiscountEligible,
                IsCreditEnabled = customer.IsCreditEnabled,
                CreditStatus = customer.CreditStatus,
                CreditLimit = customer.CreditLimit,
                CurrentBalance = customer.CurrentBalance,
                IsCreditLocked = customer.IsCreditLocked,
                IsActive = customer.IsActive,
                LoyaltyDiscountProfileId = customer.LoyaltyDiscountProfileId,
                DiscountExpiry = customer.LoyaltyDiscountExpiryDate
            };
        }

        // =========================================================
        // NORMALIZATION
        // =========================================================

        private static string NormalizeCustomerType(string? value)
        {
            string type = NormalizeText(value);

            if (type.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                return "Wholesale";

            return "Retail";
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty);
        }
    }
}