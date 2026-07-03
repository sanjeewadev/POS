namespace POS.Core.Models.Licensing
{
    public enum LicenseStatus
    {
        Missing = 0,
        Active = 1,
        ExpiringSoon = 2,
        GracePeriod = 3,
        ExpiredReadOnly = 4,
        Invalid = 5,
        Revoked = 6
    }
}