namespace POS.Cashier.AuditTests;

internal static class BackOfficeNavigationSourcePolicyAuditTests
{
    public static Task NavigationLifecycleIsControlledAsync()
    {
        VerifyRepeatedNavigationKeepsTheCurrentPage();
        VerifyDataTemplateViewsKeepTheNavigationDataContext();
        VerifyInitializationGuardsRemainRetryable();

        return Task.CompletedTask;
    }

    private static void VerifyRepeatedNavigationKeepsTheCurrentPage()
    {
        string mainViewModel = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "MainViewModel.cs");

        AuditAssert.Contains(
            mainViewModel,
            "private void NavigateTo<TPage>()",
            "shared BackOffice navigation helper");
        AuditAssert.Contains(
            mainViewModel,
            "if (CurrentPage is TPage)",
            "same-page navigation guard");
        AuditAssert.Contains(
            mainViewModel,
            "_serviceProvider.GetRequiredService<TPage>()",
            "typed page resolution");

        AuditAssert.Equal(
            43,
            CountOccurrences(mainViewModel, "private void NavigateTo"),
            "BackOffice navigation method count including the helper");
        AuditAssert.Equal(
            42,
            CountOccurrences(mainViewModel, "            NavigateTo<"),
            "BackOffice menu commands routed through the helper");
        AuditAssert.Equal(
            1,
            CountOccurrences(mainViewModel, "CurrentPage ="),
            "centralized CurrentPage assignment count");
        AuditAssert.Equal(
            1,
            CountOccurrences(mainViewModel, "GetRequiredService<"),
            "centralized page resolution count");
    }

    private static void VerifyDataTemplateViewsKeepTheNavigationDataContext()
    {
        string[] viewCodeFiles =
        {
            Path.Combine("POS.BackOffice.UI", "Views", "Pages", "Crm", "CustomerMasterView.xaml.cs"),
            Path.Combine("POS.BackOffice.UI", "Views", "Pages", "Crm", "CustomerLedgerView.xaml.cs"),
            Path.Combine("POS.BackOffice.UI", "Views", "Pages", "Crm", "GiftVoucherAdminView.xaml.cs"),
            Path.Combine("POS.BackOffice.UI", "Views", "Pages", "Reports", "FloatCashLogView.xaml.cs"),
            Path.Combine("POS.BackOffice.UI", "Views", "Pages", "Reports", "CashMovementDashboardView.xaml.cs")
        };

        foreach (string relativePath in viewCodeFiles)
        {
            string code = ReadRelative(relativePath);

            AuditAssert.False(
                code.Contains("GetRequiredService<", StringComparison.Ordinal),
                $"{relativePath} must not resolve a second ViewModel.");
            AuditAssert.False(
                code.Contains("DataContext =", StringComparison.Ordinal),
                $"{relativePath} must not replace the DataTemplate DataContext.");
            AuditAssert.False(
                code.Contains("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal),
                $"{relativePath} still imports dependency injection for ViewModel replacement.");
        }

        string giftVoucherView = ReadRelative(viewCodeFiles[2]);
        AuditAssert.Contains(
            giftVoucherView,
            "DataContext is not GiftVoucherAdminViewModel viewModel",
            "Gift Voucher inherited DataContext validation");
        AuditAssert.Contains(
            giftVoucherView,
            "await viewModel.InitializeAsync();",
            "Gift Voucher initialization on the navigation-owned ViewModel");
    }

    private static void VerifyInitializationGuardsRemainRetryable()
    {
        string[] helperBasedViewModels =
        {
            "CategoryViewModel.cs",
            "SupplierViewModel.cs",
            "UnitOfMeasureViewModel.cs"
        };

        foreach (string fileName in helperBasedViewModels)
        {
            string code = Read(
                "POS.BackOffice.UI",
                "ViewModels",
                fileName);

            AuditAssert.Contains(
                code,
                "_isInitialized = await TryLoadDataAsync();",
                $"{fileName} retry-safe initialization result");
            AuditAssert.Contains(
                code,
                "private async Task<bool> TryLoadDataAsync()",
                $"{fileName} retry-safe loading helper");
            AuditAssert.Contains(
                code,
                "return true;",
                $"{fileName} successful initialization result");
            AuditAssert.Contains(
                code,
                "return false;",
                $"{fileName} failed initialization result");
        }

        string[] guardedViewModels =
        {
            "SubCategoryViewModel.cs",
            "ItemPropertyViewModel.cs",
            "ItemMasterViewModel.cs",
            "GrnViewModel.cs",
            "TaxRateViewModel.cs"
        };

        foreach (string fileName in guardedViewModels)
        {
            string code = Read(
                "POS.BackOffice.UI",
                "ViewModels",
                fileName);
            string initializeMethod = ExtractInitializeMethod(code, fileName);

            int firstAwait = initializeMethod.IndexOf("await ", StringComparison.Ordinal);
            int successFlag = initializeMethod.IndexOf("_isInitialized = true;", StringComparison.Ordinal);

            AuditAssert.True(
                firstAwait >= 0 && successFlag > firstAwait,
                $"{fileName} marks initialization complete before loading succeeds.");
            AuditAssert.Contains(
                initializeMethod,
                "_isInitialized = false;",
                $"{fileName} initialization failure reset");
        }

        string priceHistory = Read(
            "POS.BackOffice.UI",
            "ViewModels",
            "PriceChangeHistoryViewModel.cs");
        string priceInitialize = ExtractInitializeMethod(
            priceHistory,
            "PriceChangeHistoryViewModel.cs");

        AuditAssert.Contains(
            priceInitialize,
            "await LoadLookupFiltersAsync()",
            "Price History filter initialization result");
        AuditAssert.Contains(
            priceInitialize,
            "await LoadHistoryAsync()",
            "Price History data initialization result");
        AuditAssert.Contains(
            priceInitialize,
            "IsInitialized =",
            "Price History successful initialization assignment");
        AuditAssert.Contains(
            priceInitialize,
            "&&",
            "Price History requires both filter and history loading to succeed");
        AuditAssert.Contains(
            priceHistory,
            "private async Task<bool> LoadLookupFiltersAsync()",
            "Price History retryable filter loading");
        AuditAssert.Contains(
            priceHistory,
            "private async Task<bool> LoadHistoryAsync()",
            "Price History retryable data loading");
    }

    private static string ExtractInitializeMethod(
        string source,
        string fileName)
    {
        const string signature = "private async Task InitializeAsync()";
        int start = source.IndexOf(signature, StringComparison.Ordinal);

        AuditAssert.True(
            start >= 0,
            $"InitializeAsync was not found in {fileName}.");

        int nextCommand = source.IndexOf(
            "[RelayCommand",
            start + signature.Length,
            StringComparison.Ordinal);

        int length = nextCommand >= 0
            ? nextCommand - start
            : source.Length - start;

        return source.Substring(start, length);
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));

    private static string ReadRelative(string relativePath) =>
        File.ReadAllText(
            Path.Combine(
                AuditPaths.RepositoryRoot,
                relativePath));
}
