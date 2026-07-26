using POS.Cashier.UI.Dialogs;
using POS.Cashier.UI.ViewModels;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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

            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var viewModel = new BatchSelectionViewModel(request);
                var dialog = new BatchSelectionDialog(viewModel)
                {
                    Owner = ResolveOwner()
                };

                bool? accepted = dialog.ShowDialog();
                return accepted == true
                    ? viewModel.SelectionResult
                    : null;
            });
        }

        private static Window? ResolveOwner()
        {
            Window? active = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsActive && window.IsVisible);

            return active ?? Application.Current.MainWindow;
        }
    }
}
