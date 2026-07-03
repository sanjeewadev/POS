using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.BackOffice.UI.Services;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class UnitOfMeasureViewModel : ViewModelBase
    {
        private const int MaxDisplayOrder = 9999;

        private readonly UnitOfMeasureRepository _uomRepository;
        private readonly IMessageBoxService _messageBoxService;

        private bool _isInitialized;

        private static readonly Regex UomCodeRegex =
            new("^[A-Z0-9_-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [ObservableProperty]
        private string _uomCode = string.Empty;

        [ObservableProperty]
        private string _uomDescription = string.Empty;

        [ObservableProperty]
        private bool _allowDecimals = false;

        [ObservableProperty]
        private string _displayOrderText = "0";

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private UnitOfMeasure? _selectedUom;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        public ObservableCollection<UnitOfMeasure> Uoms { get; } = new();

        public UnitOfMeasureViewModel(
            UnitOfMeasureRepository uomRepository,
            IMessageBoxService messageBoxService)
        {
            _uomRepository = uomRepository ?? throw new ArgumentNullException(nameof(uomRepository));
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                await LoadDataInternalAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load UOM records.";

                _messageBoxService.ShowError(
                    $"Failed to load UOM records:\n\n{ex.Message}",
                    "Database Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDataInternalAsync()
        {
            Uoms.Clear();

            var data = await _uomRepository.GetAllAsync(SearchText);

            foreach (var item in data)
            {
                Uoms.Add(item);
            }

            StatusMessage = $"{Uoms.Count} UOM record(s) loaded.";
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task SearchAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshAsync()
        {
            SearchText = string.Empty;
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync()
        {
            string code = SelectedUom == null
                ? NormalizeCode(UomCode)
                : NormalizeCode(SelectedUom.UomCode);

            string description = NormalizeDescription(UomDescription);

            if (!TryParseDisplayOrder(DisplayOrderText, out int displayOrder))
                return;

            if (!ValidateInput(code, description, displayOrder))
                return;

            IsBusy = true;

            try
            {
                int currentId = SelectedUom?.Id ?? 0;

                bool isCodeUnique = await _uomRepository.IsCodeUniqueAsync(code, currentId);
                if (!isCodeUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The UOM Code '{code}' is already in use.",
                        "Duplicate UOM Code");

                    return;
                }

                bool isDescriptionUnique = await _uomRepository.IsDescriptionUniqueAsync(
                    description,
                    currentId);

                if (!isDescriptionUnique)
                {
                    _messageBoxService.ShowWarning(
                        $"The UOM Description '{description}' is already in use.",
                        "Duplicate UOM Description");

                    return;
                }

                if (SelectedUom == null)
                {
                    var newUom = new UnitOfMeasure
                    {
                        UomCode = code,
                        UomDescription = description,
                        AllowDecimals = AllowDecimals,
                        DisplayOrder = displayOrder,
                        IsActive = IsActive
                    };

                    await _uomRepository.AddAsync(newUom);

                    await LoadDataInternalAsync();
                    ResetForm("Unit of Measure created successfully.");

                    _messageBoxService.ShowInformation(
                        "Unit of Measure created successfully.",
                        "Success");
                }
                else
                {
                    var updatedUom = new UnitOfMeasure
                    {
                        Id = SelectedUom.Id,

                        // UOM code is intentionally kept stable after creation.
                        UomCode = SelectedUom.UomCode,

                        UomDescription = description,
                        AllowDecimals = AllowDecimals,
                        DisplayOrder = displayOrder,
                        IsActive = IsActive
                    };

                    await _uomRepository.UpdateAsync(updatedUom);

                    await LoadDataInternalAsync();
                    ResetForm("Unit of Measure updated successfully.");

                    _messageBoxService.ShowInformation(
                        "Unit of Measure updated successfully.",
                        "Success");
                }
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Save Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                _messageBoxService.ShowError(
                    $"An error occurred while saving:\n\n{ex.Message}",
                    "Save Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            ResetForm("Ready for new Unit of Measure.");
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedUom == null)
                return;

            var selected = SelectedUom;

            IsBusy = true;

            try
            {
                var linkedData = await _uomRepository.GetLinkedDataSummaryAsync(selected.Id);

                if (linkedData.HasLinkedData)
                {
                    StatusMessage = "Delete blocked.";

                    _messageBoxService.ShowWarning(
                        linkedData.ToUserMessage(selected.UomCode),
                        "Delete Blocked");

                    return;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Linked data check failed.";

                _messageBoxService.ShowError(
                    $"Could not check linked records:\n\n{ex.Message}",
                    "Delete Check Error");

                return;
            }
            finally
            {
                IsBusy = false;
            }

            bool confirmed = _messageBoxService.ShowConfirmation(
                $"Delete Unit of Measure '{selected.UomCode}'?\n\n" +
                "This is only safe for wrongly-created or unused test UOM records.\n\n" +
                "For real business records, make the UOM inactive instead.",
                "Confirm Safe Delete",
                MessageBoxImage.Warning);

            if (!confirmed)
                return;

            IsBusy = true;

            try
            {
                await _uomRepository.DeleteAsync(selected.Id);

                await LoadDataInternalAsync();
                ResetForm("Unit of Measure deleted successfully.");

                _messageBoxService.ShowInformation(
                    "Unit of Measure deleted successfully.",
                    "Deleted");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                _messageBoxService.ShowWarning(
                    ex.Message,
                    "Delete Blocked");
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                _messageBoxService.ShowError(
                    $"An error occurred while deleting:\n\n{ex.Message}",
                    "Delete Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ResetForm(string statusMessage)
        {
            UomCode = string.Empty;
            UomDescription = string.Empty;
            AllowDecimals = false;
            DisplayOrderText = "0";
            IsActive = true;
            SelectedUom = null;
            IsCodeReadOnly = false;
            StatusMessage = statusMessage;

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnSearchTextChanged(string value)
        {
            if (_isInitialized)
            {
                StatusMessage = "Type search text and click SEARCH.";
            }
        }

        partial void OnSelectedUomChanged(UnitOfMeasure? value)
        {
            if (value != null)
            {
                UomCode = value.UomCode ?? string.Empty;
                UomDescription = value.UomDescription ?? string.Empty;
                AllowDecimals = value.AllowDecimals;
                DisplayOrderText = value.DisplayOrder.ToString(CultureInfo.InvariantCulture);
                IsActive = value.IsActive;
                IsCodeReadOnly = true;

                StatusMessage = $"Editing UOM: {value.UomCode}";
            }
            else
            {
                IsCodeReadOnly = false;
            }

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        partial void OnUomCodeChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnUomDescriptionChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnDisplayOrderTextChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            InitializeCommand.NotifyCanExecuteChanged();
            LoadDataCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanSave()
        {
            return !IsBusy &&
                   !string.IsNullOrWhiteSpace(UomCode) &&
                   !string.IsNullOrWhiteSpace(UomDescription);
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedUom != null;
        }

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeDescription(string description)
        {
            return (description ?? string.Empty).Trim();
        }

        private bool TryParseDisplayOrder(string value, out int displayOrder)
        {
            displayOrder = 0;

            string rawValue = (value ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                displayOrder = 0;
                DisplayOrderText = "0";
                return true;
            }

            bool parsed = int.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out displayOrder);

            if (!parsed)
            {
                _messageBoxService.ShowWarning(
                    "Display Order must be a whole number.",
                    "Validation Error");

                return false;
            }

            if (displayOrder < 0)
            {
                _messageBoxService.ShowWarning(
                    "Display Order cannot be negative.",
                    "Validation Error");

                return false;
            }

            if (displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning(
                    $"Display Order cannot be greater than {MaxDisplayOrder}.",
                    "Validation Error");

                return false;
            }

            return true;
        }

        private bool ValidateInput(
            string code,
            string description,
            int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                _messageBoxService.ShowWarning(
                    "UOM Code is required.",
                    "Validation Error");

                return false;
            }

            if (code.Length > 10)
            {
                _messageBoxService.ShowWarning(
                    "UOM Code cannot be longer than 10 characters.",
                    "Validation Error");

                return false;
            }

            if (!UomCodeRegex.IsMatch(code))
            {
                _messageBoxService.ShowWarning(
                    "UOM Code can only contain letters, numbers, dash, and underscore.\n\nExamples: PCS, KG, LTR, BOX",
                    "Validation Error");

                return false;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                _messageBoxService.ShowWarning(
                    "UOM Description is required.",
                    "Validation Error");

                return false;
            }

            if (description.Length > 100)
            {
                _messageBoxService.ShowWarning(
                    "UOM Description cannot be longer than 100 characters.",
                    "Validation Error");

                return false;
            }

            if (displayOrder < 0 || displayOrder > MaxDisplayOrder)
            {
                _messageBoxService.ShowWarning(
                    $"Display Order must be between 0 and {MaxDisplayOrder}.",
                    "Validation Error");

                return false;
            }

            return true;
        }
    }
}