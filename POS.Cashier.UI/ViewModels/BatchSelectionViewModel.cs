using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using POS.Cashier.UI.Services;
using POS.Core.Models.DTOs;
using POS.Core.Repositories;
using POS.Core.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace POS.Cashier.UI.ViewModels
{
    public sealed partial class BatchSelectionRow : ObservableObject
    {
        public int ItemBatchId { get; init; }
        public int ItemVariantId { get; init; }
        public string BatchNo { get; init; } = string.Empty;
        public string InternalBatchBarcode { get; init; } = string.Empty;
        public DateTime? ExpiryDate { get; init; }
        public DateTime ReceivedDate { get; init; }
        public decimal AvailableQty { get; init; }
        public decimal RetailPrice { get; init; }
        public decimal WholesalePrice { get; init; }
        public decimal ActivePrice { get; init; }
        public string PriceSource { get; init; } = string.Empty;
        public string PriceSourceText { get; init; } = string.Empty;
        public string WarningText { get; init; } = string.Empty;
        public decimal RequestedQuantity { get; init; }

        public bool HasSufficientQuantity => AvailableQty >= RequestedQuantity;
        public bool IsSelectable => HasSufficientQuantity && AvailableQty > 0m;
        public string AvailabilityText => HasSufficientQuantity
            ? QuantityDisplayFormatter.Format(AvailableQty)
            : $"{QuantityDisplayFormatter.Format(AvailableQty)} / INSUFFICIENT";
        public string ExpiryDisplayText => ExpiryDate?.ToString("yyyy-MM-dd") ?? "-";
        public string ReceivedDisplayText => ReceivedDate.ToString("yyyy-MM-dd");
    }

    public sealed partial class BatchSelectionViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private BatchSelectionRow? _selectedBatch;

        [ObservableProperty]
        private bool _isSubmitting;

        [ObservableProperty]
        private string _statusText = "Select the exact physical batch and press Enter.";

        public ObservableCollection<BatchSelectionRow> Batches { get; } = new();

        public string ItemName { get; }
        public string VariantDescription { get; }
        public string SkuCode { get; }
        public string Barcode { get; }
        public decimal RequestedQuantity { get; }
        public bool IsWholesaleMode { get; }
        public string PricingModeText => IsWholesaleMode ? "WHOLESALE" : "RETAIL";

        public CashierBatchSelectionResult? SelectionResult { get; private set; }
        public event Action<bool>? ActionCompleted;

        public BatchSelectionViewModel(CashierBatchSelectionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Item);

            ItemName = request.Item.Description;
            VariantDescription = request.Item.VariantDescription;
            SkuCode = request.Item.SkuCode;
            Barcode = request.Item.Barcode;
            RequestedQuantity = request.RequestedQuantity > 0m
                ? request.RequestedQuantity
                : 1m;
            IsWholesaleMode = request.IsWholesaleMode;

            foreach (CashierBatchDto batch in request.Batches)
            {
                Batches.Add(new BatchSelectionRow
                {
                    ItemBatchId = batch.ItemBatchId,
                    ItemVariantId = batch.ItemVariantId,
                    BatchNo = batch.BatchNo,
                    InternalBatchBarcode = batch.InternalBatchBarcode,
                    ExpiryDate = batch.ExpiryDate,
                    ReceivedDate = batch.ReceivedDate,
                    AvailableQty = batch.AvailableQty,
                    RetailPrice = batch.RetailPrice,
                    WholesalePrice = batch.WholesalePrice,
                    ActivePrice = IsWholesaleMode ? batch.WholesalePrice : batch.RetailPrice,
                    PriceSource = batch.PriceSource,
                    PriceSourceText = batch.PriceSourceText,
                    WarningText = batch.IsNearExpiry ? "NEAR EXPIRY" : string.Empty,
                    RequestedQuantity = RequestedQuantity
                });
            }

            SelectedBatch = Batches.FirstOrDefault(row => row.IsSelectable)
                ?? Batches.FirstOrDefault();
        }

        partial void OnSelectedBatchChanged(BatchSelectionRow? value)
        {
            if (value == null)
            {
                StatusText = "Select the exact physical batch.";
                return;
            }

            StatusText = value.IsSelectable
                ? $"Selected batch {value.BatchNo}. Confirm to add {QuantityDisplayFormatter.Format(RequestedQuantity)}."
                : $"Batch {value.BatchNo} cannot satisfy the requested quantity.";
        }

        private bool CanConfirm() =>
            !IsSubmitting &&
            SelectedBatch?.IsSelectable == true;

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            if (!CanConfirm() || SelectedBatch == null)
                return;

            IsSubmitting = true;
            ConfirmCommand.NotifyCanExecuteChanged();

            SelectionResult = new CashierBatchSelectionResult
            {
                ItemBatchId = SelectedBatch.ItemBatchId,
                ItemVariantId = SelectedBatch.ItemVariantId,
                RequestedQuantity = RequestedQuantity,
                ExpectedRetailPrice = SelectedBatch.RetailPrice,
                ExpectedWholesalePrice = SelectedBatch.WholesalePrice,
                ExpectedPriceSource = SelectedBatch.PriceSource
            };

            ActionCompleted?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel() => ActionCompleted?.Invoke(false);
    }
}
