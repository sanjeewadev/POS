using CommunityToolkit.Mvvm.Messaging.Messages;

namespace POS.Cashier.UI.Messages
{
    /// <summary>
    /// A decoupled message used to broadcast that a cashier has requested an item to be added to the cart.
    /// The SalesViewModel actively listens for this message in the background.
    /// </summary>
    public class AddToCartMessage : ValueChangedMessage<string>
    {
        public AddToCartMessage(string barcodeOrSku) : base(barcodeOrSku)
        {
        }
    }
}