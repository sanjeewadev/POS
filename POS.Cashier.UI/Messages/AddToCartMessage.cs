using CommunityToolkit.Mvvm.Messaging.Messages;

namespace POS.Cashier.UI.Messages
{
    public sealed class AddToCartRequest
    {
        public int ItemVariantId { get; }

        public int ItemBatchId { get; }

        public string SkuCode { get; }

        public string Barcode { get; }

        public decimal Quantity { get; }

        public bool HasVariantReference => ItemVariantId > 0;

        public bool HasBatchReference => ItemBatchId > 0;

        public bool HasBarcodeOrSku =>
            !string.IsNullOrWhiteSpace(Barcode) ||
            !string.IsNullOrWhiteSpace(SkuCode);

        // Existing compatible constructor.
        // Product Seek variant row can still send only ItemVariantId.
        public AddToCartRequest(
            int itemVariantId,
            string skuCode,
            string barcode,
            decimal quantity = 1m,
            int itemBatchId = 0)
        {
            ItemVariantId = itemVariantId;
            ItemBatchId = itemBatchId;
            SkuCode = Normalize(skuCode);
            Barcode = Normalize(barcode);
            Quantity = NormalizeQuantity(quantity);
        }

        // New exact-batch constructor.
        // Product Seek batch row should use this later.
        public AddToCartRequest(
            int itemVariantId,
            int itemBatchId,
            string skuCode,
            string barcode,
            decimal quantity = 1m)
        {
            ItemVariantId = itemVariantId;
            ItemBatchId = itemBatchId;
            SkuCode = Normalize(skuCode);
            Barcode = Normalize(barcode);
            Quantity = NormalizeQuantity(quantity);
        }

        // Scanner/manual barcode constructor.
        public AddToCartRequest(
            string barcodeOrSku,
            decimal quantity = 1m)
        {
            ItemVariantId = 0;
            ItemBatchId = 0;
            SkuCode = Normalize(barcodeOrSku);
            Barcode = Normalize(barcodeOrSku);
            Quantity = NormalizeQuantity(quantity);
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static decimal NormalizeQuantity(decimal quantity)
        {
            return quantity <= 0m ? 1m : quantity;
        }
    }

    public sealed class AddToCartMessage : ValueChangedMessage<AddToCartRequest>
    {
        public AddToCartMessage(AddToCartRequest request)
            : base(request)
        {
        }

        // Existing scanner/manual fallback.
        public AddToCartMessage(string barcodeOrSku)
            : base(new AddToCartRequest(barcodeOrSku))
        {
        }

        // Existing variant-only Product Seek fallback.
        public AddToCartMessage(
            int itemVariantId,
            string skuCode,
            string barcode,
            decimal quantity = 1m)
            : base(new AddToCartRequest(
                itemVariantId,
                skuCode,
                barcode,
                quantity))
        {
        }

        // New exact-batch Product Seek fallback.
        public AddToCartMessage(
            int itemVariantId,
            int itemBatchId,
            string skuCode,
            string barcode,
            decimal quantity = 1m)
            : base(new AddToCartRequest(
                itemVariantId,
                itemBatchId,
                skuCode,
                barcode,
                quantity))
        {
        }
    }
}