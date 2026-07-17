using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace POS.Deployment.Wizard;

internal static class NetworkAddressHelper
{
    public static string GetPreferredIpv4Address()
    {
        try
        {
            var candidates = new List<(IPAddress Address, int Score)>();

            foreach (NetworkInterface adapter in
                     NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType is
                        NetworkInterfaceType.Loopback or
                        NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                IPInterfaceProperties properties;
                try
                {
                    properties = adapter.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                bool hasIpv4Gateway = properties.GatewayAddresses.Any(
                    gateway =>
                        gateway.Address.AddressFamily ==
                            AddressFamily.InterNetwork &&
                        !gateway.Address.Equals(IPAddress.Any));

                foreach (UnicastIPAddressInformation unicast in
                         properties.UnicastAddresses)
                {
                    IPAddress address = unicast.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(address) ||
                        address.ToString().StartsWith(
                            "169.254.",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int score = 0;
                    if (hasIpv4Gateway)
                        score += 100;
                    if (IsPrivateAddress(address))
                        score += 20;
                    if (adapter.NetworkInterfaceType is
                        NetworkInterfaceType.Ethernet or
                        NetworkInterfaceType.Wireless80211)
                    {
                        score += 10;
                    }

                    candidates.Add((address, score));
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Address.ToString(),
                    StringComparer.Ordinal)
                .Select(candidate => candidate.Address.ToString())
                .FirstOrDefault()
                ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
