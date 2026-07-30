using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace POS.Cashier.UI.Services
{
    public sealed class CashierBatchSelectionRequest
    {
        public CashierSellableItemDto Item { get; init; } = null!;
        public IReadOnlyList<CashierBatchDto> Batches { get; init; } = Array.Empty<CashierBatchDto>();
        public decimal RequestedQuantity { get; init; }
        public bool IsWholesaleMode { get; init; }
    }

    public sealed class CashierBatchSelectionResult
    {
        public int ItemBatchId { get; init; }
        public int ItemVariantId { get; init; }
        public decimal RequestedQuantity { get; init; }
        public decimal ExpectedRetailPrice { get; init; }
        public decimal ExpectedWholesalePrice { get; init; }
        public string ExpectedPriceSource { get; init; } = string.Empty;
    }

    public interface ICashierBatchSelectionService
    {
        Task<CashierBatchSelectionResult?> SelectBatchAsync(
            CashierBatchSelectionRequest request);
    }

    public sealed class CashierBatchSelectionService : ICashierBatchSelectionService
    {
        public async Task<CashierBatchSelectionResult?> SelectBatchAsync(
            CashierBatchSelectionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Item);

            if (Application.Current == null)
                return null;

            try
            {
                return await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var viewModel = new BatchSelectionViewModel(request);
                        var dialog = new BatchSelectionDialog(viewModel);

                        try
                        {
                            if (Application.Current.MainWindow != null &&
                                Application.Current.MainWindow.IsVisible)
                            {
                                dialog.Owner = Application.Current.MainWindow;
                            }
                        }
                        catch (Exception exOwner)
                        {
                            // log but continue
                            LocalLogService.WriteException("Cashier", "Setting dialog.Owner", exOwner);
                        }

                        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                        bool? accepted = null;
                        try
                        {
                            accepted = dialog.ShowDialog();
                        }
                        catch (Exception exShow)
                        {
                            LocalLogService.WriteException("Cashier", "Show batch selection dialog", exShow);
                            try
                            {
                                MessageBox.Show(
                                    "Failed to open batch selection dialog.\n\n" +
                                    "Technical details were saved in the local POS Logs folder.",
                                    "Batch Dialog Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                            catch { /* swallow UI message failures */ }

                            return null;
                        }

                        return accepted == true
                            ? viewModel.SelectionResult
                            : null;
                    }
                    catch (Exception ex)
                    {
                        LocalLogService.WriteException("Cashier", "SelectBatchAsync dispatcher operation", ex);
                        try
                        {
                            MessageBox.Show(
                                "Unexpected error while preparing batch selection.\n\n" +
                                "Technical details were saved in the local POS Logs folder.",
                                "Batch Selection Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        catch { }

                        return null;
                    }
                }, DispatcherPriority.ApplicationIdle);
            }
            catch (Exception exOuter)
            {
                LocalLogService.WriteException("Cashier", "SelectBatchAsync outer", exOuter);
                return null;
            }
        }
    }
}