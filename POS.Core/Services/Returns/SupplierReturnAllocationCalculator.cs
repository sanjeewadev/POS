using System;
using POS.Core.Configuration;

namespace POS.Core.Services.Returns
{
    public sealed class SupplierReturnAllocationInput
    {
        public decimal OriginalQuantity { get; init; }
        public decimal PreviouslyReturnedQuantity { get; init; }
        public decimal RequestedQuantity { get; init; }

        public decimal OriginalCreditAmount { get; init; }
        public decimal PreviouslyReturnedCreditAmount { get; init; }

        public decimal? OriginalTaxableAmount { get; init; }
        public decimal? OriginalVatAmount { get; init; }
        public decimal? OriginalTaxInclusiveAmount { get; init; }

        public decimal PreviouslyReturnedTaxableAmount { get; init; }
        public decimal PreviouslyReturnedVatAmount { get; init; }
        public decimal PreviouslyReturnedTaxInclusiveAmount { get; init; }

        public string TaxSnapshotStatus { get; init; } = TaxSnapshotStatuses.LegacyUnknown;
    }

    public sealed class SupplierReturnAllocationResult
    {
        public decimal CreditAmount { get; init; }
        public decimal? TaxableAmount { get; init; }
        public decimal? VatAmount { get; init; }
        public decimal? TaxInclusiveAmount { get; init; }
        public string TaxSnapshotStatus { get; init; } = TaxSnapshotStatuses.LegacyUnknown;
        public bool IsFinalReturn { get; init; }
    }

    public sealed class SupplierReturnAllocationCalculator
    {
        private const decimal QuantityTolerance = 0.0005m;

        public SupplierReturnAllocationResult Calculate(SupplierReturnAllocationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (input.OriginalQuantity <= 0m)
                throw new InvalidOperationException("Original received quantity must be greater than zero.");

            if (input.PreviouslyReturnedQuantity < 0m)
                throw new InvalidOperationException("Previously returned quantity cannot be negative.");

            if (input.RequestedQuantity <= 0m)
                throw new InvalidOperationException("Supplier return quantity must be greater than zero.");

            decimal remainingQuantity = input.OriginalQuantity - input.PreviouslyReturnedQuantity;

            if (remainingQuantity < 0m)
                remainingQuantity = 0m;

            if (input.RequestedQuantity - remainingQuantity > QuantityTolerance)
                throw new InvalidOperationException("Supplier return quantity exceeds the remaining GRN quantity.");

            bool isFinal = Math.Abs(input.RequestedQuantity - remainingQuantity) <= QuantityTolerance;

            decimal credit = AllocateAmount(
                input.OriginalCreditAmount,
                input.PreviouslyReturnedCreditAmount,
                input.RequestedQuantity,
                input.OriginalQuantity,
                isFinal);

            bool hasCompleteTaxSnapshot =
                string.Equals(
                    input.TaxSnapshotStatus,
                    TaxSnapshotStatuses.Complete,
                    StringComparison.Ordinal) &&
                input.OriginalTaxableAmount.HasValue &&
                input.OriginalVatAmount.HasValue &&
                input.OriginalTaxInclusiveAmount.HasValue;

            if (!hasCompleteTaxSnapshot)
            {
                return new SupplierReturnAllocationResult
                {
                    CreditAmount = credit,
                    TaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown,
                    IsFinalReturn = isFinal
                };
            }

            decimal taxable = AllocateAmount(
                input.OriginalTaxableAmount!.Value,
                input.PreviouslyReturnedTaxableAmount,
                input.RequestedQuantity,
                input.OriginalQuantity,
                isFinal);

            decimal vat = AllocateAmount(
                input.OriginalVatAmount!.Value,
                input.PreviouslyReturnedVatAmount,
                input.RequestedQuantity,
                input.OriginalQuantity,
                isFinal);

            decimal inclusive = AllocateAmount(
                input.OriginalTaxInclusiveAmount!.Value,
                input.PreviouslyReturnedTaxInclusiveAmount,
                input.RequestedQuantity,
                input.OriginalQuantity,
                isFinal);

            return new SupplierReturnAllocationResult
            {
                CreditAmount = credit,
                TaxableAmount = taxable,
                VatAmount = vat,
                TaxInclusiveAmount = inclusive,
                TaxSnapshotStatus = TaxSnapshotStatuses.Complete,
                IsFinalReturn = isFinal
            };
        }

        private static decimal AllocateAmount(
            decimal originalAmount,
            decimal previouslyAllocated,
            decimal requestedQuantity,
            decimal originalQuantity,
            bool isFinal)
        {
            decimal residual = RoundMoney(originalAmount - previouslyAllocated);

            if (residual < 0m)
                residual = 0m;

            if (isFinal)
                return residual;

            decimal proportional = RoundMoney(
                originalAmount * requestedQuantity / originalQuantity);

            if (proportional < 0m)
                proportional = 0m;

            if (proportional > residual)
                proportional = residual;

            return proportional;
        }

        private static decimal RoundMoney(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
