using CommunityToolkit.Mvvm.Messaging.Messages;

namespace POS.Cashier.UI.Messages
{
    /// <summary>
    /// Broadcasts a message to be displayed in the Sales Screen's top notification banner.
    /// Payload: (Message Text, Hex Color Code)
    /// </summary>
    public class TopBarNotificationMessage : ValueChangedMessage<(string Message, string ColorHex)>
    {
        public TopBarNotificationMessage((string Message, string ColorHex) value) : base(value)
        {
        }
    }
}