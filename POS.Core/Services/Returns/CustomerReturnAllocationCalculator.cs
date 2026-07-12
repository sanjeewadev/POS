using System;
using POS.Core.Configuration;
using POS.Core.Models.DTOs;

namespace POS.Core.Services.Returns
{
    public sealed class CustomerReturnAllocationCalculator
    {
        private const decimal QuantityTolerance = 0.0005m;

        public CustomerReturnAllocationResult Calculate(
            CustomerReturnAllocationInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (input.SoldQuantity <= 0m)
                throw new InvalidOperationException("The original sold quantity is invalid.");

            if (input.RequestedQuantity <= 0m)
                throw new InvalidOperationException("Return quantity must be greater than zero.");

            decimal remainingQuantity = RoundQuantity(
                input.SoldQuantity - input.PreviouslyReturnedQuantity);

            if (remainingQuantity <= 0m)
                throw new InvalidOperationException("The selected sales line has already been fully returned.");

            if (input.RequestedQuantity - remainingQuantity > QuantityTolerance)
            {
                throw new InvalidOperationException(
                    $"Return quantity cannot exceed the remaining quantity of {remainingQuantity:0.###}.");
            }

            decimal requestQuantity = RoundQuantity(input.RequestedQuantity);
            bool isFinal = Math.Abs(requestQuantity - remainingQuantity) <= QuantityTolerance;
            decimal ratio = requestQuantity / input.SoldQuantity;

            decimal gross = isFinal
                ? Money(input.OriginalGrossAmount - ProportionalReturnedAmount(
                    input.OriginalGrossAmount,
                    input.PreviouslyReturnedQuantity,
                    input.SoldQuantity))
                : Money(input.OriginalGrossAmount * ratio);

            decimal refund = isFinal
                ? Money(input.OriginalRefundAmount - input.PreviouslyRefundedAmount)
                : Money(input.OriginalRefundAmount * ratio);

            if (refund < 0m)
                refund = 0m;

            decimal discount = Money(gross - refund);
            if (discount < 0m)
                discount = 0m;

            decimal? taxable = null;
            decimal? vat = null;
            decimal? inclusive = null;

            bool hasCompleteTax =
                string.Equals(
                    input.TaxSnapshotStatus,
                    TaxSnapshotStatuses.Complete,
                    StringComparison.Ordinal) &&
                input.OriginalTaxableAmount.HasValue &&
                input.OriginalVatAmount.HasValue &&
                input.OriginalTaxInclusiveAmount.HasValue;

            if (hasCompleteTax)
            {
                taxable = isFinal
                    ? Money(input.OriginalTaxableAmount!.Value - input.PreviouslyReturnedTaxableAmount)
                    : Money(input.OriginalTaxableAmount!.Value * ratio);

                vat = isFinal
                    ? Money(input.OriginalVatAmount!.Value - input.PreviouslyReturnedVatAmount)
                    : Money(input.OriginalVatAmount!.Value * ratio);

                inclusive = isFinal
                    ? Money(input.OriginalTaxInclusiveAmount!.Value - input.PreviouslyReturnedTaxInclusiveAmount)
                    : Money(input.OriginalTaxInclusiveAmount!.Value * ratio);

                taxable = Math.Max(0m, taxable.Value);
                vat = Math.Max(0m, vat.Value);
                inclusive = Math.Max(0m, inclusive.Value);

                refund = inclusive.Value;
                discount = Math.Max(0m, Money(gross - refund));
            }

            decimal unitValue = requestQuantity <= 0m
                ? 0m
                : Money(refund / requestQuantity);

            return new CustomerReturnAllocationResult
            {
                Quantity = requestQuantity,
                GrossAmount = gross,
                DiscountAmount = discount,
                RefundAmount = refund,
                RefundUnitValue = unitValue,
                TaxableAmount = taxable,
                VatAmount = vat,
                TaxInclusiveAmount = inclusive,
                IsFinalQuantity = isFinal
            };
        }

        private static decimal ProportionalReturnedAmount(
            decimal originalAmount,
            decimal returnedQuantity,
            decimal soldQuantity)
        {
            if (returnedQuantity <= 0m || soldQuantity <= 0m)
                return 0m;

            return Money(originalAmount * returnedQuantity / soldQuantity);
        }

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static decimal RoundQuantity(decimal value) =>
            decimal.Round(value, 3, MidpointRounding.AwayFromZero);
    }
}
