using CommunityToolkit.Mvvm.Messaging.Messages;

namespace POS.Cashier.UI.Messages
{
    public sealed class AddToCartRequest
    {
        public int ItemVariantId { get; }
        public string SkuCode { get; }
        public string Barcode { get; }
        public int Quantity { get; }

        public AddToCartRequest(
            int itemVariantId,
            string skuCode,
            string barcode,
            int quantity = 1)
        {
            ItemVariantId = itemVariantId;
            SkuCode = skuCode ?? string.Empty;
            Barcode = barcode ?? string.Empty;
            Quantity = quantity <= 0 ? 1 : quantity;
        }
    }

    public class AddToCartMessage : ValueChangedMessage<AddToCartRequest>
    {
        public AddToCartMessage(AddToCartRequest request) : base(request)
        {
        }

        // Backward compatibility:
        // Existing Express Item / old code that sends only barcode/SKU will still compile.
        public AddToCartMessage(string barcodeOrSku)
            : base(new AddToCartRequest(
                itemVariantId: 0,
                skuCode: barcodeOrSku,
                barcode: barcodeOrSku,
                quantity: 1))
        {
        }
    }
}