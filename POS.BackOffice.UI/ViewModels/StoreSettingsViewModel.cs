using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Services;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StoreSettingsViewModel :
        ObservableObject
    {
        private readonly StoreSettingsRepository
            _storeSettingsRepository;

        private readonly AuthService
            _authService;

        private StoreSettings?
            _loadedSettings;

        public StoreSettingsViewModel(
            StoreSettingsRepository
                storeSettingsRepository,
            AuthService authService)
        {
            _storeSettingsRepository =
                storeSettingsRepository;

            _authService = authService;
        }

        // =====================================================
        // 1. STORE IDENTITY
        // =====================================================

        [ObservableProperty]
        private string _legalName =
            string.Empty;

        [ObservableProperty]
        private string _storeName =
            string.Empty;

        // =====================================================
        // 2. ADDRESS & CONTACT
        // =====================================================

        [ObservableProperty]
        private string _addressLine1 =
            string.Empty;

        [ObservableProperty]
        private string _addressLine2 =
            string.Empty;

        [ObservableProperty]
        private string _city =
            string.Empty;

        [ObservableProperty]
        private string _postalCode =
            string.Empty;

        [ObservableProperty]
        private string _country =
            "Sri Lanka";

        [ObservableProperty]
        private string _phone =
            string.Empty;

        [ObservableProperty]
        private string _email =
            string.Empty;

        // =====================================================
        // 3. BUSINESS / VAT DETAILS
        // =====================================================

        [ObservableProperty]
        private string _brn =
            string.Empty;

        [ObservableProperty]
        private string _taxNo =
            string.Empty;

        // =====================================================
        // 4. RECEIPT INFORMATION
        // =====================================================

        [ObservableProperty]
        private string _receiptHeader =
            string.Empty;

        [ObservableProperty]
        private string _receiptFooter =
            "Thank You! Come Again.";

        // =====================================================
        // 5. PAGE STATUS
        // =====================================================

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage =
            "Ready.";

        [ObservableProperty]
        private string _statusColor =
            "#666666";

        [ObservableProperty]
        private string _lastUpdatedText =
            "Not saved yet.";

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;

                SetStatus(
                    "Loading store settings...",
                    "#003366");

                StoreSettings settings =
                    await _storeSettingsRepository
                        .GetOrCreateDefaultAsync();

                _loadedSettings = settings;

                ApplySettings(settings);

                SetStatus(
                    "Store settings loaded.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Load Store Settings",
                    ex);

                SetStatus(
                    "Store settings could not be loaded. " +
                    "Technical details were saved in the local POS Logs folder.",
                    "#B91C1C");
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

            if (_loadedSettings == null)
            {
                SetStatus(
                    "Load the store settings before saving.",
                    "#B91C1C");

                return;
            }

            string validationMessage =
                ValidateInputs();

            if (!string.IsNullOrWhiteSpace(
                    validationMessage))
            {
                SetStatus(
                    validationMessage,
                    "#B91C1C");

                return;
            }

            try
            {
                IsBusy = true;

                SetStatus(
                    "Saving store settings...",
                    "#003366");

                ApplyEditableValuesToModel(
                    _loadedSettings);

                StoreSettings saved =
                    await _storeSettingsRepository
                        .SaveAsync(
                            _loadedSettings,
                            GetCurrentUserName());

                _loadedSettings = saved;

                ApplySettings(saved);

                SetStatus(
                    "Store settings saved. " +
                    "The next receipt will use the updated information.",
                    "#008000");
            }
            catch (Exception ex)
            {
                LocalLogService.WriteException(
                    "BackOffice",
                    "Save Store Settings",
                    ex);

                SetStatus(
                    $"Store settings were not saved: " +
                    $"{GetFriendlyMessage(ex)}",
                    "#B91C1C");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DiscardChangesAsync()
        {
            await LoadAsync();
        }

        private void ApplySettings(
            StoreSettings settings)
        {
            LegalName =
                Normalize(settings.LegalName);

            StoreName =
                Normalize(settings.StoreName);

            AddressLine1 =
                Normalize(settings.AddressLine1);

            AddressLine2 =
                Normalize(settings.AddressLine2);

            City =
                Normalize(settings.City);

            PostalCode =
                Normalize(settings.PostalCode);

            Country =
                string.IsNullOrWhiteSpace(
                    settings.Country)
                    ? "Sri Lanka"
                    : settings.Country.Trim();

            Phone =
                Normalize(settings.Phone);

            Email =
                Normalize(settings.Email);

            Brn =
                Normalize(settings.Brn);

            TaxNo =
                Normalize(settings.TaxNo);

            ReceiptHeader =
                NormalizeMultiline(
                    settings.ReceiptHeader);

            ReceiptFooter =
                NormalizeMultiline(
                    settings.ReceiptFooter);

            if (string.IsNullOrWhiteSpace(
                    ReceiptFooter))
            {
                ReceiptFooter =
                    "Thank You! Come Again.";
            }

            LastUpdatedText =
                settings.UpdatedAt.HasValue
                    ? $"{settings.UpdatedAt.Value:yyyy-MM-dd HH:mm} " +
                      $"by {DisplayOrSystem(settings.UpdatedBy)}"
                    : $"Created {settings.CreatedAt:yyyy-MM-dd HH:mm}";
        }

        private void ApplyEditableValuesToModel(
            StoreSettings settings)
        {
            // Only the fields exposed on this final page are changed.
            // Hidden legacy fields remain untouched for compatibility.
            settings.LegalName = LegalName;
            settings.StoreName = StoreName;

            settings.AddressLine1 = AddressLine1;
            settings.AddressLine2 = AddressLine2;
            settings.City = City;
            settings.PostalCode = PostalCode;
            settings.Country = Country;
            settings.Phone = Phone;
            settings.Email = Email;

            settings.Brn = Brn;
            settings.TaxNo = TaxNo;

            settings.ReceiptHeader = ReceiptHeader;
            settings.ReceiptFooter = ReceiptFooter;
        }

        private string ValidateInputs()
        {
            string legalName =
                Normalize(LegalName);

            string storeName =
                Normalize(StoreName);

            if (string.IsNullOrWhiteSpace(
                    legalName))
            {
                return "Business / legal name is required.";
            }

            if (string.IsNullOrWhiteSpace(
                    storeName))
            {
                return "Store / trading name is required.";
            }

            if (legalName.Length > 200)
            {
                return "Business / legal name cannot exceed 200 characters.";
            }

            if (storeName.Length > 150)
            {
                return "Store / trading name cannot exceed 150 characters.";
            }

            if (Normalize(Brn).Length > 100)
            {
                return "Business registration number cannot exceed 100 characters.";
            }

            if (Normalize(TaxNo).Length > 100)
            {
                return "VAT registration number cannot exceed 100 characters.";
            }

            if (Normalize(AddressLine1).Length > 250 ||
                Normalize(AddressLine2).Length > 250)
            {
                return "Each address line cannot exceed 250 characters.";
            }

            if (Normalize(City).Length > 100 ||
                Normalize(Country).Length > 100)
            {
                return "City and country cannot exceed 100 characters.";
            }

            if (Normalize(PostalCode).Length > 50)
            {
                return "Postal code cannot exceed 50 characters.";
            }

            if (Normalize(Phone).Length > 100)
            {
                return "Telephone cannot exceed 100 characters.";
            }

            string email =
                Normalize(Email);

            if (email.Length > 150)
            {
                return "Email cannot exceed 150 characters.";
            }

            if (!string.IsNullOrWhiteSpace(email) &&
                (!email.Contains('@') ||
                 email.StartsWith('@') ||
                 email.EndsWith('@')))
            {
                return "Enter a valid email address or leave it blank.";
            }

            if (NormalizeMultiline(
                    ReceiptHeader).Length > 1000)
            {
                return "Receipt header cannot exceed 1,000 characters.";
            }

            if (NormalizeMultiline(
                    ReceiptFooter).Length > 1000)
            {
                return "Receipt footer cannot exceed 1,000 characters.";
            }

            return string.Empty;
        }

        private string GetCurrentUserName()
        {
            return string.IsNullOrWhiteSpace(
                _authService
                    .CurrentUser
                    ?.Username)
                ? "Administrator"
                : _authService
                    .CurrentUser
                    .Username;
        }

        private static string GetFriendlyMessage(
            Exception exception)
        {
            if (exception is
                InvalidOperationException &&
                !string.IsNullOrWhiteSpace(
                    exception.Message))
            {
                return exception.Message;
            }

            return
                "An unexpected error occurred. " +
                "Technical details were saved in the local POS Logs folder.";
        }

        private void SetStatus(
            string message,
            string color)
        {
            StatusMessage =
                string.IsNullOrWhiteSpace(message)
                    ? "Ready."
                    : message;

            StatusColor =
                string.IsNullOrWhiteSpace(color)
                    ? "#666666"
                    : color;
        }

        private static string Normalize(
            string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeMultiline(
            string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();
        }

        private static string DisplayOrSystem(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "System"
                : value.Trim();
        }
    }
}
