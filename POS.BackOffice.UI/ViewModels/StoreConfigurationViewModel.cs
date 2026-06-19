using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using POS.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;

namespace POS.BackOffice.UI.ViewModels
{
    public partial class StoreConfigurationViewModel : ViewModelBase
    {
        private readonly SystemSettingsRepository _repository;

        // ==========================================
        // 1. CORPORATE IDENTITY & BRANDING
        // ==========================================
        [ObservableProperty] private string _legalName = string.Empty;
        [ObservableProperty] private string _storeName = string.Empty; // Trading Name
        [ObservableProperty] private string _brn = string.Empty;
        [ObservableProperty] private string _taxNo = string.Empty;

        // ==========================================
        // 2. CONTACT DATA
        // ==========================================
        [ObservableProperty] private string _address1 = string.Empty;
        [ObservableProperty] private string _city = string.Empty;
        [ObservableProperty] private string _postalCode = string.Empty;
        [ObservableProperty] private string _country = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _mobile = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _website = string.Empty;

        // ==========================================
        // 3. DOCUMENT FORMATTING
        // ==========================================
        [ObservableProperty] private string _invPrefix = string.Empty;
        [ObservableProperty] private string _quotePrefix = string.Empty;
        [ObservableProperty] private string _poPrefix = string.Empty;
        [ObservableProperty] private string _receiptHeader = string.Empty;
        [ObservableProperty] private string _receiptFooter = string.Empty;
        [ObservableProperty] private string _invoiceTerms = string.Empty;

        // ==========================================
        // 4. HARDWARE: WORKSTATION IDENTITY
        // ==========================================
        [ObservableProperty] private string _terminalName = string.Empty;
        [ObservableProperty] private string _location = string.Empty;

        // ==========================================
        // 5. HARDWARE: PRINTER & DRAWER
        // ==========================================
        [ObservableProperty] private string _printerMode = string.Empty;
        [ObservableProperty] private string _targetPrinter = string.Empty;
        [ObservableProperty] private string _paperWidth = string.Empty;

        [ObservableProperty] private bool _enableDrawer;
        [ObservableProperty] private string _drawerBrand = string.Empty;
        [ObservableProperty] private string _kickCode = string.Empty;

        // ==========================================
        // 6. HARDWARE: PERIPHERALS (SCANNERS, SCALES, DISPLAYS)
        // ==========================================
        [ObservableProperty] private string _scanSuffix = string.Empty;

        [ObservableProperty] private bool _enableScale;
        [ObservableProperty] private string _scalePort = string.Empty;

        [ObservableProperty] private bool _enablePoleDisplay;
        [ObservableProperty] private string _poleCom = string.Empty;
        [ObservableProperty] private string _poleWelcome = string.Empty;

        [ObservableProperty] private bool _enableEftpos;

        // ==========================================
        // 7. SYSTEM LOCALIZATION & MATH
        // ==========================================
        [ObservableProperty] private string _timeZone = string.Empty;
        [ObservableProperty] private string _dateFormat = string.Empty;
        [ObservableProperty] private string _finYearStart = string.Empty;
        [ObservableProperty] private decimal _globalVatRate;


        public StoreConfigurationViewModel(SystemSettingsRepository repository)
        {
            _repository = repository;
            _ = LoadSettingsAsync();
        }

        // ==============================================================================
        // ACTION: UNPACK (Database -> ViewModel)
        // ==============================================================================
        [RelayCommand]
        private async Task LoadSettingsAsync()
        {
            try
            {
                var dict = await _repository.LoadAllSettingsAsync();

                // Helper function to safely extract strings without crashing if a key is missing
                string GetString(string key) => dict.TryGetValue(key, out var val) ? val : string.Empty;

                // Helper functions to safely convert Strings to native C# types
                bool GetBool(string key) => dict.TryGetValue(key, out var val) && bool.TryParse(val, out var b) && b;
                decimal GetDecimal(string key) => dict.TryGetValue(key, out var val) && decimal.TryParse(val, out var d) ? d : 0m;

                LegalName = GetString("LegalName");
                StoreName = GetString("StoreName");
                Brn = GetString("BRN");
                TaxNo = GetString("TaxNo");

                Address1 = GetString("Address1");
                City = GetString("City");
                PostalCode = GetString("PostalCode");
                Country = GetString("Country");
                Phone = GetString("Phone");
                Mobile = GetString("Mobile");
                Email = GetString("Email");
                Website = GetString("Website");

                InvPrefix = GetString("InvPrefix");
                QuotePrefix = GetString("QuotePrefix");
                PoPrefix = GetString("PoPrefix");
                ReceiptHeader = GetString("ReceiptHeader");
                ReceiptFooter = GetString("ReceiptFooter");
                InvoiceTerms = GetString("InvoiceTerms");

                TerminalName = GetString("TerminalName");
                Location = GetString("Location");

                PrinterMode = GetString("PrinterMode");
                TargetPrinter = GetString("TargetPrinter");
                PaperWidth = GetString("PaperWidth");

                EnableDrawer = GetBool("EnableDrawer");
                DrawerBrand = GetString("DrawerBrand");
                KickCode = GetString("KickCode");

                ScanSuffix = GetString("ScanSuffix");

                EnableScale = GetBool("EnableScale");
                ScalePort = GetString("ScalePort");

                EnablePoleDisplay = GetBool("EnablePoleDisplay");
                PoleCom = GetString("PoleCom");
                PoleWelcome = GetString("PoleWelcome");

                EnableEftpos = GetBool("EnableEftpos");

                TimeZone = GetString("TimeZone");
                DateFormat = GetString("DateFormat");
                FinYearStart = GetString("FinYearStart");
                GlobalVatRate = GetDecimal("GlobalVatRate");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load configuration: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==============================================================================
        // ACTION: PACK (ViewModel -> Database)
        // ==============================================================================
        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                var dict = new Dictionary<string, string>
                {
                    { "LegalName", LegalName },
                    { "StoreName", StoreName },
                    { "BRN", Brn },
                    { "TaxNo", TaxNo },
                    { "Address1", Address1 },
                    { "City", City },
                    { "PostalCode", PostalCode },
                    { "Country", Country },
                    { "Phone", Phone },
                    { "Mobile", Mobile },
                    { "Email", Email },
                    { "Website", Website },
                    { "InvPrefix", InvPrefix },
                    { "QuotePrefix", QuotePrefix },
                    { "PoPrefix", PoPrefix },
                    { "ReceiptHeader", ReceiptHeader },
                    { "ReceiptFooter", ReceiptFooter },
                    { "InvoiceTerms", InvoiceTerms },
                    { "TerminalName", TerminalName },
                    { "Location", Location },
                    { "PrinterMode", PrinterMode },
                    { "TargetPrinter", TargetPrinter },
                    { "PaperWidth", PaperWidth },
                    { "EnableDrawer", EnableDrawer.ToString() },
                    { "DrawerBrand", DrawerBrand },
                    { "KickCode", KickCode },
                    { "ScanSuffix", ScanSuffix },
                    { "EnableScale", EnableScale.ToString() },
                    { "ScalePort", ScalePort },
                    { "EnablePoleDisplay", EnablePoleDisplay.ToString() },
                    { "PoleCom", PoleCom },
                    { "PoleWelcome", PoleWelcome },
                    { "EnableEftpos", EnableEftpos.ToString() },
                    { "TimeZone", TimeZone },
                    { "DateFormat", DateFormat },
                    { "FinYearStart", FinYearStart },
                    { "GlobalVatRate", GlobalVatRate.ToString("0.00") }
                };

                await _repository.SaveSettingsAsync(dict);

                MessageBox.Show("Store Configuration and Hardware Profile saved successfully!", "Configuration Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==============================================================================
        // IT DIAGNOSTIC COMMANDS
        // ==============================================================================
        [RelayCommand]
        private void DiscardChanges()
        {
            var result = MessageBox.Show("Are you sure you want to discard unsaved changes?", "Discard Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _ = LoadSettingsAsync();
            }
        }

        [RelayCommand]
        private void TestPrint()
        {
            MessageBox.Show($"Sending generic ESC/POS test print payload to '{TargetPrinter}'...", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void KickDrawer()
        {
            MessageBox.Show($"Sending RJ11 Kick Code [{KickCode}] to '{TargetPrinter}'...", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void TestDisplay()
        {
            MessageBox.Show($"Sending welcome string '{PoleWelcome}' to {PoleCom}...", "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}