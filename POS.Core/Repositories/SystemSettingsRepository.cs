using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class SystemSettingsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SystemSettingsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. READ ALL (Loads into a fast Dictionary for the UI)
        // ==============================================================================
        public async Task<Dictionary<string, string>> LoadAllSettingsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // SECURITY CHECK: If the table is totally empty (first boot), inject defaults.
            if (!await context.SystemSettings.AnyAsync())
            {
                await SeedDefaultSettingsAsync(context);
            }

            // Return the entire table as a Key/Value Dictionary
            return await context.SystemSettings
                .AsNoTracking()
                .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
        }

        // ==============================================================================
        // 2. WRITE ALL (Loops through UI changes and updates the Database)
        // ==============================================================================
        public async Task SaveSettingsAsync(Dictionary<string, string> settingsToSave)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            foreach (var kvp in settingsToSave)
            {
                var existingSetting = await context.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == kvp.Key);

                if (existingSetting != null)
                {
                    existingSetting.SettingValue = kvp.Value;
                    existingSetting.LastUpdated = DateTime.Now;
                }
                else
                {
                    // If a brand new setting was invented, add it automatically
                    context.SystemSettings.Add(new SystemSetting
                    {
                        SettingKey = kvp.Key,
                        SettingValue = kvp.Value,
                        LastUpdated = DateTime.Now
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        // ==============================================================================
        // 3. THE SEEDER (Default values for a fresh installation)
        // ==============================================================================
        private async Task SeedDefaultSettingsAsync(AppDbContext context)
        {
            var defaults = new List<SystemSetting>
            {
                // 1. Identity & Contact
                new() { SettingKey = "StoreName", SettingValue = "Apex Supermart" },
                new() { SettingKey = "LegalName", SettingValue = "Apex Retail Solutions Pvt Ltd" },
                new() { SettingKey = "BRN", SettingValue = "PV-123456" },
                new() { SettingKey = "TaxNo", SettingValue = "VAT-987654321-V" },
                new() { SettingKey = "Address1", SettingValue = "No. 45, Main Street" },
                new() { SettingKey = "City", SettingValue = "Colombo 04" },
                new() { SettingKey = "PostalCode", SettingValue = "00400" },
                new() { SettingKey = "Country", SettingValue = "Sri Lanka" },
                new() { SettingKey = "Phone", SettingValue = "+94 11 234 5678" },
                new() { SettingKey = "Mobile", SettingValue = "+94 77 123 4567" },
                new() { SettingKey = "Email", SettingValue = "support@apexsupermart.com" },
                new() { SettingKey = "Website", SettingValue = "www.apexsupermart.com" },
                
                // 2. Formatting
                new() { SettingKey = "InvPrefix", SettingValue = "INV-" },
                new() { SettingKey = "QuotePrefix", SettingValue = "QTN-" },
                new() { SettingKey = "PoPrefix", SettingValue = "PO-" },
                new() { SettingKey = "ReceiptHeader", SettingValue = "Welcome to Apex Supermart!\nServing you since 1995." },
                new() { SettingKey = "ReceiptFooter", SettingValue = "Thank you for shopping with us!\nGoods once sold cannot be returned without this receipt." },
                new() { SettingKey = "InvoiceTerms", SettingValue = "1. Payment is due within 30 days.\n2. Late payments incur 2% interest." },
                
                // 3. Hardware
                new() { SettingKey = "TerminalName", SettingValue = "MAIN-TILL-01" },
                new() { SettingKey = "Location", SettingValue = "Main Store" },
                new() { SettingKey = "PrinterMode", SettingValue = "Windows Spooler (USB/Driver)" },
                new() { SettingKey = "TargetPrinter", SettingValue = "EPSON TM-T82 Receipt" },
                new() { SettingKey = "PaperWidth", SettingValue = "80mm (Standard)" },
                new() { SettingKey = "EnableDrawer", SettingValue = "True" },
                new() { SettingKey = "DrawerBrand", SettingValue = "Epson / Generic ESC/POS" },
                new() { SettingKey = "KickCode", SettingValue = "27,112,0,25,250" },
                new() { SettingKey = "ScanSuffix", SettingValue = "Auto-Enter / Add Item Instantly" },
                new() { SettingKey = "EnableScale", SettingValue = "False" },
                new() { SettingKey = "ScalePort", SettingValue = "COM3" },
                new() { SettingKey = "EnablePoleDisplay", SettingValue = "True" },
                new() { SettingKey = "PoleCom", SettingValue = "COM4" },
                new() { SettingKey = "PoleWelcome", SettingValue = "Welcome to Apex!" },
                new() { SettingKey = "EnableEftpos", SettingValue = "False" },
                
                // 4. Localization & Math
                new() { SettingKey = "TimeZone", SettingValue = "(UTC+05:30) Sri Jayawardenepura" },
                new() { SettingKey = "DateFormat", SettingValue = "DD/MM/YYYY (UK/Asia Default)" },
                new() { SettingKey = "FinYearStart", SettingValue = "April (Apr-Mar)" },
                
                // THE DYNAMIC MATH VARIABLE
                new() { SettingKey = "GlobalVatRate", SettingValue = "15.00" }
            };

            context.SystemSettings.AddRange(defaults);
            await context.SaveChangesAsync();
        }
    }
}