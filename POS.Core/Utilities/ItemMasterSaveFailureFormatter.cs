using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Core.Utilities
{
    public static class ItemMasterSaveFailureFormatter
    {
        private const string GenericEfMessage =
            "An error occurred while saving the entity changes";

        public static string GetUserMessage(Exception? exception)
        {
            if (exception == null)
            {
                return "The item could not be saved. No data was committed.";
            }

            List<string> messages = EnumerateMessages(exception)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string combined = string.Join(" | ", messages);

            if (ContainsAny(
                    combined,
                    "IX_ItemParents_ItemCode",
                    "ItemParents.ItemCode",
                    "duplicate item code"))
            {
                return "The item code already exists. Choose a different item-code suffix.";
            }

            if (ContainsAny(
                    combined,
                    "IX_ItemVariants_SkuCode",
                    "ItemVariants.SkuCode",
                    "duplicate SKU"))
            {
                return "One or more generated SKUs already exist. Regenerate the matrix or correct the duplicate SKU before saving.";
            }

            if (ContainsAny(
                    combined,
                    "IX_ItemVariants_Barcode",
                    "ItemVariants.Barcode",
                    "duplicate barcode"))
            {
                return "One or more item barcodes already exist. Correct or regenerate the duplicate barcode before saving.";
            }

            if (ContainsAny(
                    combined,
                    "ItemBatches.InternalBatchBarcode",
                    "GRN batch barcode"))
            {
                return "One or more item barcodes already exist as GRN batch barcodes. Use a different item barcode.";
            }

            string? meaningful = messages.FirstOrDefault(message =>
                !message.StartsWith(
                    GenericEfMessage,
                    StringComparison.OrdinalIgnoreCase) &&
                !message.Equals(
                    "See the inner exception for details.",
                    StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(meaningful))
                return meaningful;

            return
                "The database rejected the item values. No data was committed. " +
                "Review the item code, generated SKUs, barcodes, and supplier assignments, then try again.";
        }

        private static IEnumerable<string> EnumerateMessages(
            Exception exception)
        {
            for (Exception? current = exception;
                 current != null;
                 current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                    yield return current.Message;
            }
        }

        private static bool ContainsAny(
            string source,
            params string[] markers)
        {
            return markers.Any(marker =>
                source.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
