using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace POS.Cashier.UI.ViewModels
{
    public partial class BatchSelectionViewModel : ObservableObject
    {
        public ObservableCollection<CashierBatchDto> Batches { get; } = new();

        [ObservableProperty]
        private CashierBatchDto? _selectedBatch;

        [ObservableProperty]
        private string _itemDescription = string.Empty;

        [ObservableProperty]
        private decimal _requestedQty = 1m;

        [ObservableProperty]
        private string _statusText = "Select a stock batch.";

        [ObservableProperty]
        private string _statusColorHex = "#2563EB";

        public event Action<bool>? ActionCompleted;

        public BatchSelectionViewModel(
            string itemDescription,
            IEnumerable<CashierBatchDto> batches,
            decimal requestedQty)
        {
            ItemDescription = itemDescription ?? string.Empty;
            RequestedQty = requestedQty <= 0m ? 1m : requestedQty;

            LoadBatches(batches);

            SelectedBatch = Batches.FirstOrDefault(b =>
                b.IsSelectable &&
                b.AvailableQty >= RequestedQty);

            RefreshStatusForCurrentState();
        }

        partial void OnSelectedBatchChanged(CashierBatchDto? value)
        {
            ConfirmCommand.NotifyCanExecuteChanged();
            RefreshStatusForSelectedBatch(value);
        }

        partial void OnRequestedQtyChanged(decimal value)
        {
            ConfirmCommand.NotifyCanExecuteChanged();
            RefreshStatusForSelectedBatch(SelectedBatch);
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            if (!CanConfirm())
            {
                RefreshStatusForSelectedBatch(SelectedBatch);
                return;
            }

            ActionCompleted?.Invoke(true);
        }

        private bool CanConfirm()
        {
            return SelectedBatch != null &&
                   SelectedBatch.IsSelectable &&
                   SelectedBatch.AvailableQty >= RequestedQty;
        }

        [RelayCommand]
        private void Cancel()
        {
            ActionCompleted?.Invoke(false);
        }

        private void LoadBatches(IEnumerable<CashierBatchDto>? batches)
        {
            Batches.Clear();

            if (batches == null)
                return;

            foreach (var batch in batches
                         .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                         .ThenBy(b => b.ExpiryDate)
                         .ThenBy(b => b.ReceivedDate)
                         .ThenBy(b => b.BatchNo))
            {
                Batches.Add(batch);
            }
        }

        private void RefreshStatusForCurrentState()
        {
            if (Batches.Count == 0)
            {
                StatusText = "No sellable stock batch found.";
                StatusColorHex = "#B91C1C";
                return;
            }

            if (SelectedBatch == null)
            {
                bool hasAnySelectableBatch = Batches.Any(b => b.IsSelectable);

                if (!hasAnySelectableBatch)
                {
                    StatusText = "No selectable batch is available.";
                    StatusColorHex = "#B91C1C";
                    return;
                }

                StatusText = "No batch has enough stock for the requested quantity.";
                StatusColorHex = "#D97706";
                return;
            }

            RefreshStatusForSelectedBatch(SelectedBatch);
        }

        private void RefreshStatusForSelectedBatch(CashierBatchDto? batch)
        {
            if (Batches.Count == 0)
            {
                StatusText = "No sellable stock batch found.";
                StatusColorHex = "#B91C1C";
                return;
            }

            if (batch == null)
            {
                StatusText = "Select a stock batch.";
                StatusColorHex = "#2563EB";
                return;
            }

            if (batch.IsExpired)
            {
                StatusText = "Expired batch cannot be sold.";
                StatusColorHex = "#B91C1C";
                return;
            }

            if (!batch.IsSelectable)
            {
                StatusText = "This batch is not selectable.";
                StatusColorHex = "#B91C1C";
                return;
            }

            if (batch.AvailableQty < RequestedQty)
            {
                StatusText =
                    $"Only {FormatQuantity(batch.AvailableQty)} available. Requested quantity is {FormatQuantity(RequestedQty)}.";

                StatusColorHex = "#D97706";
                return;
            }

            if (batch.IsNearExpiry)
            {
                StatusText = $"Near expiry batch selected. Expiry: {batch.ExpiryDisplayText}.";
                StatusColorHex = "#D97706";
                return;
            }

            string batchNo = string.IsNullOrWhiteSpace(batch.BatchDisplayText)
                ? "-"
                : batch.BatchDisplayText;

            StatusText = $"Selected batch {batchNo}. Tap row or press Enter to select.";
            StatusColorHex = "#166534";
        }

        private static string FormatQuantity(decimal value)
        {
            return value.ToString("0.###");
        }
    }
}