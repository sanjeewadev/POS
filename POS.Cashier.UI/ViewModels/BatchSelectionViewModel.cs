using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Core.Models.DTOs;

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
        private string _statusText = "Select the batch to sell.";

        [ObservableProperty]
        private string _statusColorHex = "#003366";

        public event Action<bool>? ActionCompleted;

        public BatchSelectionViewModel(
            string itemDescription,
            IEnumerable<CashierBatchDto> batches,
            decimal requestedQty)
        {
            ItemDescription = itemDescription ?? string.Empty;
            RequestedQty = requestedQty <= 0 ? 1m : requestedQty;

            foreach (var batch in batches.OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
                                         .ThenBy(b => b.ExpiryDate)
                                         .ThenBy(b => b.ReceivedDate)
                                         .ThenBy(b => b.BatchNo))
            {
                Batches.Add(batch);
            }

            SelectedBatch = Batches.FirstOrDefault(b =>
                b.IsSelectable &&
                b.AvailableQty >= RequestedQty);

            if (Batches.Count == 0)
            {
                StatusText = "No sellable batch stock found.";
                StatusColorHex = "#DC3545";
            }
            else if (SelectedBatch == null)
            {
                StatusText = "No batch has enough stock for the requested quantity.";
                StatusColorHex = "#D97706";
            }
        }

        partial void OnSelectedBatchChanged(CashierBatchDto? value)
        {
            ConfirmCommand.NotifyCanExecuteChanged();

            if (value == null)
            {
                StatusText = "Select the batch to sell.";
                StatusColorHex = "#003366";
                return;
            }

            if (value.IsExpired)
            {
                StatusText = "Expired batch cannot be sold.";
                StatusColorHex = "#DC3545";
                return;
            }

            if (value.AvailableQty < RequestedQty)
            {
                StatusText = $"Only {value.AvailableQty:N3} available in this batch.";
                StatusColorHex = "#D97706";
                return;
            }

            if (value.IsNearExpiry)
            {
                StatusText = $"Near expiry batch selected: {value.ExpiryDisplayText}";
                StatusColorHex = "#D97706";
                return;
            }

            StatusText = $"Selected batch: {value.BatchNo}";
            StatusColorHex = "#10B981";
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            if (!CanConfirm())
                return;

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
    }
}