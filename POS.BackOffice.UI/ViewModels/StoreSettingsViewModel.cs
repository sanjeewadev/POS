using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StoreSettingsViewModel : ObservableObject
    {
        private readonly StoreSettingsRepository _repository;

        private int _settingsId;
        private DateTime _createdAt;

        public StoreSettingsViewModel(StoreSettingsRepository repository)
        {
            _repository = repository;

            DateFormats = new ObservableCollection<string>
            {
                "dd/MM/yyyy",
                "MM/dd/yyyy",
                "yyyy-MM-dd"
            };

            FinancialYearStartMonths = new ObservableCollection<int>(
                Enumerable.Range(1, 12));

            TimeZones = new ObservableCollection<string>
            {
                "Sri Lanka Standard Time",
                "India Standard Time",
                "UTC",
                "GMT Standard Time"
            };

            _ = LoadAsync();
        }

        public ObservableCollection<string> DateFormats { get; }

        public ObservableCollection<int> FinancialYearStartMonths { get; }

        public ObservableCollection<string> TimeZones { get; }

        // =========================================================
        // COMPANY / STORE IDENTITY
        // =========================================================

        [ObservableProperty]
        private string _legalName = string.Empty;

        [ObservableProperty]
        private string _storeName = string.Empty;

        [ObservableProperty]
        private string _brn = string.Empty;

        [ObservableProperty]
        private string _taxNo = string.Empty;

        [ObservableProperty]
        private string _addressLine1 = string.Empty;

        [ObservableProperty]
        private string _addressLine2 = string.Empty;

        [ObservableProperty]
        private string _city = string.Empty;

        [ObservableProperty]
        private string _postalCode = string.Empty;

        [ObservableProperty]
        private string _country = "Sri Lanka";

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        // =========================================================
        // TAX / CURRENCY
        // =========================================================

        [ObservableProperty]
        private decimal _globalVatRate = 0m;

        [ObservableProperty]
        private string _currencyCode = "LKR";

        [ObservableProperty]
        private string _currencySymbol = "Rs.";

        // =========================================================
        // DOCUMENT PREFIXES
        // =========================================================

        [ObservableProperty]
        private string _invoicePrefix = "INV";

        [ObservableProperty]
        private string _purchaseOrderPrefix = "PO";

        [ObservableProperty]
        private string _quotationPrefix = "QT";

        // =========================================================
        // RECEIPT / INVOICE TEXT
        // =========================================================

        [ObservableProperty]
        private string _receiptHeader = string.Empty;

        [ObservableProperty]
        private string _receiptFooter = "Thank You! Come Again.";

        [ObservableProperty]
        private string _invoiceTerms = string.Empty;

        // =========================================================
        // REGIONAL / FINANCIAL
        // =========================================================

        [ObservableProperty]
        private string _timeZoneId = "Sri Lanka Standard Time";

        [ObservableProperty]
        private string _dateFormat = "dd/MM/yyyy";

        [ObservableProperty]
        private int _financialYearStartMonth = 1;

        // =========================================================
        // STATUS
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        [ObservableProperty]
        private string _statusColor = "#64748B";

        // =========================================================
        // COMPATIBILITY PROPERTIES FOR OLD DRAFT BINDINGS
        // These allow old StoreConfigurationView bindings to work
        // until the new StoreSettingsView.xaml is created.
        // =========================================================

        public string Address1
        {
            get => AddressLine1;
            set
            {
                AddressLine1 = value;
                OnPropertyChanged();
            }
        }

        public string InvPrefix
        {
            get => InvoicePrefix;
            set
            {
                InvoicePrefix = value;
                OnPropertyChanged();
            }
        }

        public string PoPrefix
        {
            get => PurchaseOrderPrefix;
            set
            {
                PurchaseOrderPrefix = value;
                OnPropertyChanged();
            }
        }

        public string QuotePrefix
        {
            get => QuotationPrefix;
            set
            {
                QuotationPrefix = value;
                OnPropertyChanged();
            }
        }

        public string TimeZone
        {
            get => TimeZoneId;
            set
            {
                TimeZoneId = value;
                OnPropertyChanged();
            }
        }

        public int FinYearStart
        {
            get => FinancialYearStartMonth;
            set
            {
                FinancialYearStartMonth = value;
                OnPropertyChanged();
            }
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Loading store settings...", "#3B82F6");

                StoreSettings settings = await _repository.GetOrCreateDefaultAsync();

                ApplySettingsToViewModel(settings);

                SetStatus("Store settings loaded.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load store settings: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                SetStatus("Saving store settings...", "#3B82F6");

                StoreSettings settings = BuildSettingsFromViewModel();

                StoreSettings savedSettings = await _repository.SaveAsync(
                    settings,
                    "BackOffice");

                ApplySettingsToViewModel(savedSettings);

                SetStatus("Store settings saved successfully.", "#10B981");
            }
            catch (Exception ex)
            {
                SetStatus($"Save failed: {ex.Message}", "#EF4444");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DiscardChangesAsync()
        {
            if (IsBusy)
                return;

            await LoadAsync();
        }

        // =========================================================
        // LOAD / SAVE MAPPING
        // =========================================================

        private void ApplySettingsToViewModel(StoreSettings settings)
        {
            if (settings == null)
                settings = StoreSettingsRepository.CreateDefaultSettings();

            _settingsId = settings.Id;
            _createdAt = settings.CreatedAt;

            LegalName = settings.LegalName;
            StoreName = settings.StoreName;
            Brn = settings.Brn;
            TaxNo = settings.TaxNo;

            AddressLine1 = settings.AddressLine1;
            AddressLine2 = settings.AddressLine2;
            City = settings.City;
            PostalCode = settings.PostalCode;
            Country = settings.Country;
            Phone = settings.Phone;
            Email = settings.Email;

            GlobalVatRate = settings.GlobalVatRate;
            CurrencyCode = settings.CurrencyCode;
            CurrencySymbol = settings.CurrencySymbol;

            InvoicePrefix = settings.InvoicePrefix;
            PurchaseOrderPrefix = settings.PurchaseOrderPrefix;
            QuotationPrefix = settings.QuotationPrefix;

            ReceiptHeader = settings.ReceiptHeader;
            ReceiptFooter = settings.ReceiptFooter;
            InvoiceTerms = settings.InvoiceTerms;

            TimeZoneId = settings.TimeZoneId;
            DateFormat = settings.DateFormat;
            FinancialYearStartMonth = settings.FinancialYearStartMonth;

            RaiseCompatibilityPropertyChanges();
        }

        private StoreSettings BuildSettingsFromViewModel()
        {
            return new StoreSettings
            {
                Id = _settingsId,
                CreatedAt = _createdAt == default ? DateTime.Now : _createdAt,

                LegalName = LegalName,
                StoreName = StoreName,
                Brn = Brn,
                TaxNo = TaxNo,

                AddressLine1 = AddressLine1,
                AddressLine2 = AddressLine2,
                City = City,
                PostalCode = PostalCode,
                Country = Country,
                Phone = Phone,
                Email = Email,

                GlobalVatRate = GlobalVatRate,
                CurrencyCode = CurrencyCode,
                CurrencySymbol = CurrencySymbol,

                InvoicePrefix = InvoicePrefix,
                PurchaseOrderPrefix = PurchaseOrderPrefix,
                QuotationPrefix = QuotationPrefix,

                ReceiptHeader = ReceiptHeader,
                ReceiptFooter = ReceiptFooter,
                InvoiceTerms = InvoiceTerms,

                TimeZoneId = TimeZoneId,
                DateFormat = DateFormat,
                FinancialYearStartMonth = FinancialYearStartMonth,

                IsActive = true
            };
        }

        private void RaiseCompatibilityPropertyChanges()
        {
            OnPropertyChanged(nameof(Address1));
            OnPropertyChanged(nameof(InvPrefix));
            OnPropertyChanged(nameof(PoPrefix));
            OnPropertyChanged(nameof(QuotePrefix));
            OnPropertyChanged(nameof(TimeZone));
            OnPropertyChanged(nameof(FinYearStart));
        }

        private void SetStatus(string message, string color)
        {
            StatusMessage = message;
            StatusColor = color;
        }
    }
}