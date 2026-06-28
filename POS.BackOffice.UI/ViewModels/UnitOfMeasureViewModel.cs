using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models;
using POS.Core.Repositories;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class UnitOfMeasureViewModel : ViewModelBase
    {
        private readonly UnitOfMeasureRepository _uomRepository;

        private static readonly Regex UomCodeRegex =
            new Regex("^[A-Z0-9_-]+$", RegexOptions.Compiled);

        // =========================================================
        // FORM FIELDS
        // =========================================================

        [ObservableProperty]
        private string _uomCode = string.Empty;

        [ObservableProperty]
        private string _uomDescription = string.Empty;

        [ObservableProperty]
        private bool _allowDecimals = false;

        [ObservableProperty]
        private int _displayOrder = 0;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private UnitOfMeasure? _selectedUom;

        [ObservableProperty]
        private bool _isCodeReadOnly = false;

        // =========================================================
        // FILTER FIELDS
        // =========================================================

        [ObservableProperty]
        private string _searchText = string.Empty;

        // =========================================================
        // UI STATE
        // =========================================================

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private string _statusMessage = "Ready.";

        // =========================================================
        // DATA COLLECTION
        // =========================================================

        public ObservableCollection<UnitOfMeasure> Uoms { get; } = new();

        public UnitOfMeasureViewModel(UnitOfMeasureRepository uomRepository)
        {
            _uomRepository = uomRepository;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task LoadDataAsync()
        {
            IsBusy = true;

            try
            {
                Uoms.Clear();

                var data = await _uomRepository.GetAllAsync(SearchText);

                foreach (var item in data)
                {
                    Uoms.Add(item);
                }

                StatusMessage = $"{Uoms.Count} UOM record(s) loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Failed to load UOM records.";

                MessageBox.Show(
                    $"Failed to load UOM records:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
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
            string code = NormalizeCode(UomCode);
            string description = NormalizeDescription(UomDescription);

            if (!ValidateInput(code, description, DisplayOrder))
                return;

            IsBusy = true;

            try
            {
                if (SelectedUom == null)
                {
                    bool isCodeUnique = await _uomRepository.IsCodeUniqueAsync(code, 0);

                    if (!isCodeUnique)
                    {
                        MessageBox.Show(
                            $"The UOM Code '{code}' is already in use.",
                            "Duplicate UOM Code",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    bool isDescriptionUnique = await _uomRepository.IsDescriptionUniqueAsync(description, 0);

                    if (!isDescriptionUnique)
                    {
                        MessageBox.Show(
                            $"The UOM Description '{description}' is already in use.",
                            "Duplicate UOM Description",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    var newUom = new UnitOfMeasure
                    {
                        UomCode = code,
                        UomDescription = description,
                        AllowDecimals = AllowDecimals,
                        DisplayOrder = DisplayOrder,
                        IsActive = IsActive
                    };

                    await _uomRepository.AddAsync(newUom);

                    StatusMessage = "Unit of Measure created successfully.";
                }
                else
                {
                    bool isDescriptionUnique = await _uomRepository.IsDescriptionUniqueAsync(
                        description,
                        SelectedUom.Id);

                    if (!isDescriptionUnique)
                    {
                        MessageBox.Show(
                            $"The UOM Description '{description}' is already in use.",
                            "Duplicate UOM Description",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    var updatedUom = new UnitOfMeasure
                    {
                        Id = SelectedUom.Id,

                        // UOM code is intentionally kept stable after creation.
                        UomCode = SelectedUom.UomCode,

                        UomDescription = description,
                        AllowDecimals = AllowDecimals,
                        DisplayOrder = DisplayOrder,
                        IsActive = IsActive
                    };

                    await _uomRepository.UpdateAsync(updatedUom);

                    StatusMessage = "Unit of Measure updated successfully.";
                }

                await LoadDataAsync();
                Clear();

                MessageBox.Show(
                    "Unit of Measure saved successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Save blocked.";

                MessageBox.Show(
                    ex.Message,
                    "Save Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Save failed.";

                MessageBox.Show(
                    $"An error occurred while saving:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Clear()
        {
            UomCode = string.Empty;
            UomDescription = string.Empty;
            AllowDecimals = false;
            DisplayOrder = 0;
            IsActive = true;
            SelectedUom = null;
            IsCodeReadOnly = false;

            StatusMessage = "Ready for new Unit of Measure.";

            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteAsync()
        {
            if (SelectedUom == null)
                return;

            var result = MessageBox.Show(
                $"Delete Unit of Measure '{SelectedUom.UomCode}'?\n\n" +
                "This only works if the UOM is not assigned to item records.\n\n" +
                "If it is already used, uncheck 'Unit is Active' and click SAVE.",
                "Confirm Safe Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsBusy = true;

            try
            {
                await _uomRepository.DeleteAsync(SelectedUom.Id);

                await LoadDataAsync();
                Clear();

                StatusMessage = "Unit of Measure deleted successfully.";

                MessageBox.Show(
                    "Unit of Measure deleted successfully.",
                    "Deleted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = "Delete blocked.";

                MessageBox.Show(
                    $"{ex.Message}\n\nTo hide this UOM from new item creation, uncheck 'Unit is Active' and click SAVE.",
                    "Delete Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                StatusMessage = "Delete failed.";

                MessageBox.Show(
                    $"An error occurred while deleting:\n\n{ex.Message}",
                    "Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // PROPERTY CHANGE HANDLERS
        // =========================================================

        partial void OnSearchTextChanged(string value)
        {
            // Do not hit the database on every key press.
            StatusMessage = "Type search text and click SEARCH.";
        }

        partial void OnSelectedUomChanged(UnitOfMeasure? value)
        {
            if (value != null)
            {
                UomCode = value.UomCode ?? string.Empty;
                UomDescription = value.UomDescription ?? string.Empty;
                AllowDecimals = value.AllowDecimals;
                DisplayOrder = value.DisplayOrder;
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

        partial void OnDisplayOrderChanged(int value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsBusyChanged(bool value)
        {
            LoadDataCommand.NotifyCanExecuteChanged();
            SearchCommand.NotifyCanExecuteChanged();
            RefreshCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }

        // =========================================================
        // COMMAND STATE
        // =========================================================

        private bool CanRunCommand()
        {
            return !IsBusy;
        }

        private bool CanSave()
        {
            return !IsBusy;
        }

        private bool CanDelete()
        {
            return !IsBusy && SelectedUom != null;
        }

        // =========================================================
        // VALIDATION HELPERS
        // =========================================================

        private static string NormalizeCode(string code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeDescription(string description)
        {
            return (description ?? string.Empty).Trim();
        }

        private static bool ValidateInput(string code, string description, int displayOrder)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(
                    "UOM Code is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (code.Length > 10)
            {
                MessageBox.Show(
                    "UOM Code cannot be longer than 10 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (!UomCodeRegex.IsMatch(code))
            {
                MessageBox.Show(
                    "UOM Code can only contain letters, numbers, dash, and underscore.\n\nExamples: PCS, KG, LTR, BOX",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show(
                    "UOM Description is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (description.Length > 100)
            {
                MessageBox.Show(
                    "UOM Description cannot be longer than 100 characters.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (displayOrder < 0)
            {
                MessageBox.Show(
                    "Display Order cannot be negative.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (displayOrder > 9999)
            {
                MessageBox.Show(
                    "Display Order is too large.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}