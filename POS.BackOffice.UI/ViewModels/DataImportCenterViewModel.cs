using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.DTOs.Import;
using POS.Core.Repositories;
using POS.Core.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Globalization;
using CsvHelper;
using Microsoft.Win32;
using POS.Core.Services;
using POS.BackOffice.UI.Services;
using System.Linq;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class DataImportCenterViewModel : ViewModelBase
    {
        private readonly ItemMasterRepository _itemMasterRepository;
        private readonly CategoryRepository _categoryRepository;
        private readonly SubCategoryRepository _subCategoryRepository;
        private readonly SupplierRepository _supplierRepository;
        private readonly UnitOfMeasureRepository _uomRepository;
        private readonly TaxRateRepository _taxRateRepository;
        private readonly IMessageBoxService _messageBoxService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStep1Visible))]
        [NotifyPropertyChangedFor(nameof(IsStep2Visible))]
        [NotifyPropertyChangedFor(nameof(IsStep3Visible))]
        private int _currentStep = 1;

        public bool IsStep1Visible => CurrentStep == 1;
        public bool IsStep2Visible => CurrentStep == 2;
        public bool IsStep3Visible => CurrentStep == 3;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsItemsImport))]
        [NotifyPropertyChangedFor(nameof(IsCategoriesImport))]
        [NotifyPropertyChangedFor(nameof(IsSubCategoriesImport))]
        [NotifyPropertyChangedFor(nameof(IsSuppliersImport))]
        private string _selectedImportType = "Suppliers"; // Defaulting for testing

        public bool IsItemsImport => SelectedImportType == "Items";
        public bool IsCategoriesImport => SelectedImportType == "Categories";
        public bool IsSubCategoriesImport => SelectedImportType == "SubCategories";
        public bool IsSuppliersImport => SelectedImportType == "Suppliers";

        public ObservableCollection<string> ImportTypes { get; } = new() { "Items", "Categories", "SubCategories", "Suppliers" };

        public ObservableCollection<ItemPreviewRow> PreviewData { get; } = new();
        public ObservableCollection<CategoryPreviewRow> CategoriesPreviewData { get; } = new();
        public ObservableCollection<SubCategoryPreviewRow> SubCategoriesPreviewData { get; } = new();
        public ObservableCollection<SupplierPreviewRow> SuppliersPreviewData { get; } = new();

        [ObservableProperty]
        private int _totalRows;

        [ObservableProperty]
        private int _validRows;

        [ObservableProperty]
        private int _errorRows;

        [ObservableProperty]
        private bool _hasErrors;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExecuteImportCommand))]
        private bool _canImport;

        public DataImportCenterViewModel(
            ItemMasterRepository itemMasterRepository, 
            CategoryRepository categoryRepository, 
            SubCategoryRepository subCategoryRepository,
            SupplierRepository supplierRepository,
            UnitOfMeasureRepository uomRepository,
            TaxRateRepository taxRateRepository,
            IMessageBoxService messageBoxService)
        {
            _itemMasterRepository = itemMasterRepository;
            _categoryRepository = categoryRepository;
            _subCategoryRepository = subCategoryRepository;
            _supplierRepository = supplierRepository;
            _uomRepository = uomRepository;
            _taxRateRepository = taxRateRepository;
            _messageBoxService = messageBoxService;
        }

        [RelayCommand]
        private void DownloadTemplate()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"{SelectedImportType}ImportTemplate.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(dialog.FileName);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                
                if (IsItemsImport)
                {
                    csv.WriteHeader<ItemImportDto>();
                }
                else if (IsCategoriesImport)
                {
                    csv.WriteHeader<CategoryImportDto>();
                }
                else if (IsSubCategoriesImport)
                {
                    csv.WriteHeader<SubCategoryImportDto>();
                }
                else if (IsSuppliersImport)
                {
                    csv.WriteHeader<SupplierImportDto>();
                }
                
                csv.NextRecord();
                _messageBoxService.ShowInformation("Template downloaded successfully.", "Success");
            }
        }

        [RelayCommand]
        private async Task UploadFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    PreviewData.Clear();
                    CategoriesPreviewData.Clear();
                    SubCategoriesPreviewData.Clear();
                    SuppliersPreviewData.Clear();

                    using var reader = new StreamReader(dialog.FileName);
                    using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                    
                    if (IsItemsImport)
                    {
                        var records = csv.GetRecords<ItemImportDto>().ToList();
                        int rowNum = 1;
                        var existingCategories = await _categoryRepository.GetAllAsync();
                        var existingSubCategories = await _subCategoryRepository.GetAllAsync();

                        foreach (var record in records)
                        {
                            var row = new ItemPreviewRow { RowNumber = rowNum++, Data = record };
                            
                            if (string.IsNullOrWhiteSpace(record.ItemName))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Item Name is required.";
                            }
                            else if (string.IsNullOrWhiteSpace(record.CategoryName))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Category Name is required.";
                            }
                            else
                            {
                                var cat = existingCategories.FirstOrDefault(c => c.CategoryName.Equals(record.CategoryName, StringComparison.OrdinalIgnoreCase));
                                if (cat == null)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = $"Category '{record.CategoryName}' not found.";
                                }
                                else if (cat.IsDeactivated)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = $"Category '{record.CategoryName}' is deactivated.";
                                }
                                else if (!string.IsNullOrWhiteSpace(record.SubCategoryName))
                                {
                                    var subCat = existingSubCategories.FirstOrDefault(s => s.CategoryId == cat.Id && s.SubCategoryName.Equals(record.SubCategoryName, StringComparison.OrdinalIgnoreCase));
                                    if (subCat == null)
                                    {
                                        row.IsValid = false;
                                        row.ErrorMessage = $"SubCategory '{record.SubCategoryName}' not found under '{cat.CategoryName}'.";
                                    }
                                    else if (subCat.IsDeactivated)
                                    {
                                        row.IsValid = false;
                                        row.ErrorMessage = $"SubCategory '{record.SubCategoryName}' is deactivated.";
                                    }
                                }
                            }

                            if (row.IsValid && !string.IsNullOrWhiteSpace(record.Barcode))
                            {
                                if (records.Count(r => r.Barcode?.Equals(record.Barcode, StringComparison.OrdinalIgnoreCase) == true) > 1)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Duplicate Barcode in this file.";
                                }
                                else if (!await _itemMasterRepository.IsBarcodeUniqueAcrossItemsAndBatchesAsync(record.Barcode))
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Barcode already exists in system.";
                                }
                            }

                            if (row.IsValid && !string.IsNullOrWhiteSpace(record.ItemCode))
                            {
                                if (records.Count(r => r.ItemCode?.Equals(record.ItemCode, StringComparison.OrdinalIgnoreCase) == true) > 1)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Duplicate Item Code in this file.";
                                }
                                else if (!await _itemMasterRepository.IsItemCodeUniqueAsync(record.ItemCode))
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Item Code already exists in system.";
                                }
                            }
                            PreviewData.Add(row);
                        }
                    }
                    else if (IsCategoriesImport)
                    {
                        var records = csv.GetRecords<CategoryImportDto>().ToList();
                        int rowNum = 1;
                        var existingCategories = await _categoryRepository.GetAllAsync();

                        foreach (var record in records)
                        {
                            var row = new CategoryPreviewRow { RowNumber = rowNum++, Data = record };
                            
                            if (string.IsNullOrWhiteSpace(record.CategoryName))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Category Name is required.";
                            }
                            else if (records.Count(r => r.CategoryName?.Equals(record.CategoryName, StringComparison.OrdinalIgnoreCase) == true) > 1)
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Duplicate Category Name in this file.";
                            }
                            else if (existingCategories.Any(c => c.CategoryName.Equals(record.CategoryName, StringComparison.OrdinalIgnoreCase)))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Category Name already exists in system.";
                            }
                            else if (!string.IsNullOrWhiteSpace(record.CategoryCode))
                            {
                                if (records.Count(r => r.CategoryCode?.Equals(record.CategoryCode, StringComparison.OrdinalIgnoreCase) == true) > 1)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Duplicate Category Code in this file.";
                                }
                                else if (existingCategories.Any(c => c.CategoryCode.Equals(record.CategoryCode, StringComparison.OrdinalIgnoreCase)))
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Category Code already exists in system.";
                                }
                            }

                            CategoriesPreviewData.Add(row);
                        }
                    }
                    else if (IsSubCategoriesImport)
                    {
                        var records = csv.GetRecords<SubCategoryImportDto>().ToList();
                        int rowNum = 1;
                        var existingCategories = await _categoryRepository.GetAllAsync();
                        var existingSubCategories = await _subCategoryRepository.GetAllAsync();

                        foreach (var record in records)
                        {
                            var row = new SubCategoryPreviewRow { RowNumber = rowNum++, Data = record };
                            
                            if (string.IsNullOrWhiteSpace(record.ParentCategoryName) || string.IsNullOrWhiteSpace(record.SubCategoryName))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Both Parent Category Name and SubCategory Name are required.";
                            }
                            else
                            {
                                var parentCat = existingCategories.FirstOrDefault(c => c.CategoryName.Equals(record.ParentCategoryName, StringComparison.OrdinalIgnoreCase));
                                if (parentCat == null)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = $"Parent Category '{record.ParentCategoryName}' not found.";
                                }
                                else if (parentCat.IsDeactivated)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = $"Parent Category '{record.ParentCategoryName}' is deactivated.";
                                }
                                else
                                {
                                    if (records.Count(r => r.ParentCategoryName?.Equals(record.ParentCategoryName, StringComparison.OrdinalIgnoreCase) == true &&
                                                           r.SubCategoryName?.Equals(record.SubCategoryName, StringComparison.OrdinalIgnoreCase) == true) > 1)
                                    {
                                        row.IsValid = false;
                                        row.ErrorMessage = "Duplicate SubCategory Name for this Parent in this file.";
                                    }
                                    else if (existingSubCategories.Any(s => s.CategoryId == parentCat.Id && s.SubCategoryName.Equals(record.SubCategoryName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        row.IsValid = false;
                                        row.ErrorMessage = "SubCategory already exists under this Parent in system.";
                                    }
                                    else if (!string.IsNullOrWhiteSpace(record.SubCategoryCode))
                                    {
                                        if (records.Count(r => r.SubCategoryCode?.Equals(record.SubCategoryCode, StringComparison.OrdinalIgnoreCase) == true) > 1)
                                        {
                                            row.IsValid = false;
                                            row.ErrorMessage = "Duplicate SubCategory Code in this file.";
                                        }
                                        else if (existingSubCategories.Any(s => s.SubCategoryCode.Equals(record.SubCategoryCode, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            row.IsValid = false;
                                            row.ErrorMessage = "SubCategory Code already exists in system.";
                                        }
                                    }
                                }
                            }

                            SubCategoriesPreviewData.Add(row);
                        }
                    }
                    else if (IsSuppliersImport)
                    {
                        var records = csv.GetRecords<SupplierImportDto>().ToList();
                        int rowNum = 1;
                        var existingSuppliers = await _supplierRepository.GetAllAsync();

                        foreach (var record in records)
                        {
                            var row = new SupplierPreviewRow { RowNumber = rowNum++, Data = record };

                            // 1. Format check
                            if (string.IsNullOrWhiteSpace(record.SupplierName))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Supplier Name is required.";
                            }
                            // 2. File-level check
                            else if (records.Count(r => r.SupplierName?.Equals(record.SupplierName, StringComparison.OrdinalIgnoreCase) == true) > 1)
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Duplicate Supplier Name in this file.";
                            }
                            // 3. Database-level check
                            else if (existingSuppliers.Any(s => s.SupplierName.Equals(record.SupplierName, StringComparison.OrdinalIgnoreCase)))
                            {
                                row.IsValid = false;
                                row.ErrorMessage = "Supplier Name already exists in system.";
                            }
                            // 4. Code check if provided
                            else if (!string.IsNullOrWhiteSpace(record.SupplierCode))
                            {
                                if (records.Count(r => r.SupplierCode?.Equals(record.SupplierCode, StringComparison.OrdinalIgnoreCase) == true) > 1)
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Duplicate Supplier Code in this file.";
                                }
                                else if (existingSuppliers.Any(s => s.SupplierCode.Equals(record.SupplierCode, StringComparison.OrdinalIgnoreCase)))
                                {
                                    row.IsValid = false;
                                    row.ErrorMessage = "Supplier Code already exists in system.";
                                }
                            }

                            SuppliersPreviewData.Add(row);
                        }
                    }

                    UpdateStats();
                    CurrentStep = 2; // Move to preview
                }
                catch (Exception ex)
                {
                    _messageBoxService.ShowError($"Error reading file: {ex.Message}", "Import Error");
                }
            }
        }

        private void UpdateStats()
        {
            if (IsItemsImport)
            {
                TotalRows = PreviewData.Count;
                ErrorRows = PreviewData.Count(r => !r.IsValid);
            }
            else if (IsCategoriesImport)
            {
                TotalRows = CategoriesPreviewData.Count;
                ErrorRows = CategoriesPreviewData.Count(r => !r.IsValid);
            }
            else if (IsSubCategoriesImport)
            {
                TotalRows = SubCategoriesPreviewData.Count;
                ErrorRows = SubCategoriesPreviewData.Count(r => !r.IsValid);
            }
            else if (IsSuppliersImport)
            {
                TotalRows = SuppliersPreviewData.Count;
                ErrorRows = SuppliersPreviewData.Count(r => !r.IsValid);
            }
            else
            {
                TotalRows = 0;
                ErrorRows = 0;
            }

            ValidRows = TotalRows - ErrorRows;
            HasErrors = ErrorRows > 0;
            CanImport = TotalRows > 0 && ErrorRows == 0;
        }

        [RelayCommand(CanExecute = nameof(CanImport))]
        private async Task ExecuteImportAsync()
        {
            try
            {
                CurrentStep = 3; // Move to success/processing
                
                if (IsItemsImport)
                {
                    var existingCategories = await _categoryRepository.GetAllAsync();
                    var existingSubCategories = await _subCategoryRepository.GetAllAsync();
                    
                    var uoms = await _uomRepository.GetAllAsync();
                    var defaultUom = uoms.FirstOrDefault(u => u.UomCode.Equals("Pcs", StringComparison.OrdinalIgnoreCase)) 
                                     ?? uoms.FirstOrDefault();
                    
                    var taxCategories = await _taxRateRepository.GetApprovedCategoriesAsync();
                    var defaultTaxCat = taxCategories.FirstOrDefault(t => t.CategoryCode.Equals("OutOfScope", StringComparison.OrdinalIgnoreCase) || t.CategoryCode.Equals("TaxFree", StringComparison.OrdinalIgnoreCase))
                                        ?? taxCategories.FirstOrDefault();

                    if (defaultUom == null) throw new InvalidOperationException("No Unit of Measure found in the database. Please create one first.");
                    if (defaultTaxCat == null) throw new InvalidOperationException("No Tax Category found in the database. Please create one first.");

                    var groupedItems = PreviewData.GroupBy(r => r.Data.ItemName?.Trim(), StringComparer.OrdinalIgnoreCase);
                    var matrices = new List<(ItemParent Parent, List<ItemVariant> Variants)>();

                    foreach (var group in groupedItems)
                    {
                        var firstRow = group.First().Data;
                        
                        var cat = existingCategories.First(c => c.CategoryName.Equals(firstRow.CategoryName, StringComparison.OrdinalIgnoreCase));
                        var subCat = string.IsNullOrWhiteSpace(firstRow.SubCategoryName) ? null : 
                                     existingSubCategories.FirstOrDefault(s => s.CategoryId == cat.Id && s.SubCategoryName.Equals(firstRow.SubCategoryName, StringComparison.OrdinalIgnoreCase));

                        string pCode = (firstRow.ItemCode ?? string.Empty).Trim();

                        var parent = new ItemParent
                        {
                            ItemCode = pCode,
                            ItemName = firstRow.ItemName,
                            CategoryId = cat.Id,
                            SubCategoryId = subCat?.Id,
                            UnitOfMeasureId = defaultUom.Id,
                            TaxCategoryId = defaultTaxCat.Id,
                            TaxCode = defaultTaxCat.CategoryCode,
                            ItemType = POS.Core.Configuration.ItemTypeCodes.StockItem,
                            HasBatchTracking = true
                        };

                        var variants = new List<ItemVariant>();
                        foreach (var row in group)
                        {
                            string finalSku = string.Empty;
                            if (group.Count() == 1 && !string.IsNullOrWhiteSpace(pCode))
                            {
                                finalSku = pCode;
                            }

                            var variant = new ItemVariant
                            {
                                SkuCode = finalSku,
                                VariantDescription = "Standard",
                                Barcode = row.Data.Barcode ?? string.Empty,
                                CostPrice = row.Data.CostPrice ?? 0m,
                                RetailPrice = row.Data.RetailPrice ?? 0m,
                                WholesalePrice = row.Data.WholesalePrice ?? 0m
                            };
                            variants.Add(variant);
                        }

                        matrices.Add((parent, variants));
                    }
                    
                    await _itemMasterRepository.SaveFullMatricesAsync(matrices);
                }
                else if (IsCategoriesImport)
                {
                    var categories = CategoriesPreviewData.Select(row => new Category
                    {
                        CategoryCode = row.Data.CategoryCode,
                        CategoryName = row.Data.CategoryName,
                        Description = row.Data.Description ?? string.Empty,
                        DisplayOrder = row.Data.DisplayOrder ?? 0
                    }).ToList();

                    await _categoryRepository.AddBulkAsync(categories);
                }
                else if (IsSubCategoriesImport)
                {
                    var existingCategories = await _categoryRepository.GetAllAsync();
                    var subCategories = new List<SubCategory>();
                    
                    foreach (var row in SubCategoriesPreviewData)
                    {
                        var parentCat = existingCategories.FirstOrDefault(c => c.CategoryName.Equals(row.Data.ParentCategoryName, StringComparison.OrdinalIgnoreCase));
                        
                        if (parentCat != null)
                        {
                            subCategories.Add(new SubCategory
                            {
                                CategoryId = parentCat.Id,
                                SubCategoryCode = row.Data.SubCategoryCode,
                                SubCategoryName = row.Data.SubCategoryName,
                                DisplayOrder = row.Data.DisplayOrder ?? 0
                            });
                        }
                    }

                    await _subCategoryRepository.AddBulkAsync(subCategories);
                }
                else if (IsSuppliersImport)
                {
                    var suppliers = SuppliersPreviewData.Select(row => new Supplier
                    {
                        SupplierCode = row.Data.SupplierCode,
                        SupplierName = row.Data.SupplierName,
                        CompanyName = row.Data.CompanyName ?? string.Empty,
                        ContactPerson = row.Data.ContactPerson ?? string.Empty,
                        Phone1 = row.Data.Phone1 ?? string.Empty,
                        Phone2 = row.Data.Phone2 ?? string.Empty,
                        Email = row.Data.Email ?? string.Empty,
                        Address = row.Data.Address ?? string.Empty,
                        HasVat = false,
                        VatNumber = string.Empty,
                        DefaultCreditDays = 30,
                        IsDeactivated = false,
                        CurrentBalance = 0m
                    }).ToList();

                    await _supplierRepository.AddBulkAsync(suppliers);
                }

                _messageBoxService.ShowInformation($"{ValidRows} records imported successfully!", "Import Complete");
                
                PreviewData.Clear();
                CategoriesPreviewData.Clear();
                SubCategoriesPreviewData.Clear();
                SuppliersPreviewData.Clear();
                CurrentStep = 1;
            }
            catch (Exception ex)
            {
                _messageBoxService.ShowError($"Import failed: {ex.Message}", "Error");
                CurrentStep = 2;
            }
        }

        [RelayCommand]
        private void CancelImport()
        {
            PreviewData.Clear();
            CategoriesPreviewData.Clear();
            SubCategoriesPreviewData.Clear();
            SuppliersPreviewData.Clear();
            CurrentStep = 1;
        }

        [RelayCommand]
        private void RemoveRow(object row)
        {
            if (row is ItemPreviewRow itemRow)
            {
                PreviewData.Remove(itemRow);
            }
            else if (row is CategoryPreviewRow catRow)
            {
                CategoriesPreviewData.Remove(catRow);
            }
            else if (row is SubCategoryPreviewRow subCatRow)
            {
                SubCategoriesPreviewData.Remove(subCatRow);
            }
            else if (row is SupplierPreviewRow supRow)
            {
                SuppliersPreviewData.Remove(supRow);
            }
            UpdateStats();
        }

        [RelayCommand]
        private void ClearErrors()
        {
            if (IsItemsImport)
            {
                var errors = PreviewData.Where(r => !r.IsValid).ToList();
                foreach (var err in errors) PreviewData.Remove(err);
            }
            else if (IsCategoriesImport)
            {
                var errors = CategoriesPreviewData.Where(r => !r.IsValid).ToList();
                foreach (var err in errors) CategoriesPreviewData.Remove(err);
            }
            else if (IsSubCategoriesImport)
            {
                var errors = SubCategoriesPreviewData.Where(r => !r.IsValid).ToList();
                foreach (var err in errors) SubCategoriesPreviewData.Remove(err);
            }
            else if (IsSuppliersImport)
            {
                var errors = SuppliersPreviewData.Where(r => !r.IsValid).ToList();
                foreach (var err in errors) SuppliersPreviewData.Remove(err);
            }
            UpdateStats();
        }
    }
}


