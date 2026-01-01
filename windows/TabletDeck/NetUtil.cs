using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TabletDeck;

internal static class NetUtil
{
    public static IReadOnlyList<string> GetLocalIpv4Candidates()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Ppp &&
                !IsLikelyVpnAdapter(ni))
            .SelectMany(ni =>
            {
                var props = ni.GetIPProperties();
                return props.UnicastAddresses
                    .Where(ua =>
                        ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ua.Address) &&
                        !IsLinkLocal169(ua.Address))
                    .Select(ua => new { Ip = ua.Address.ToString(), Score = ScoreInterface(ni, props) });
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Ip)
            .Distinct()
            .ToList();

        if (candidates.Count != 0)
            return candidates;

        // Fallback: anything IPv4 that's up (helps on uncommon adapters).
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
            .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
            .Select(ua => ua.Address.ToString())
            .Distinct()
            .ToList();
    }

    public static string GetBestLocalIpv4OrLoopback(string? exclude = null)
    {
        var list = GetLocalIpv4Candidates();
        if (string.IsNullOrWhiteSpace(exclude))
            return list.FirstOrDefault() ?? "127.0.0.1";

        return list.FirstOrDefault(ip => !ip.Equals(exclude)) ?? (list.FirstOrDefault() ?? "127.0.0.1");
    }

    private static bool IsLinkLocal169(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 169 && b[1] == 254;
    }

    private static bool IsLikelyVpnAdapter(NetworkInterface ni)
    {
        var s = $"{ni.Name} {ni.Description}".ToLowerInvariant();

        return s.Contains("vpn")
               || s.Contains("wireguard")
               || s.Contains("wintun")
               || s.Contains("openvpn")
               || s.Contains("tap")
               || s.Contains("tun")
               || s.Contains("tailscale")
               || s.Contains("zerotier")
               || s.Contains("hamachi")
               || s.Contains("cisco")
               || s.Contains("fortinet")
               || s.Contains("pulse secure");
    }

    private static int ScoreInterface(NetworkInterface ni, IPInterfaceProperties props)
    {
        var score = 0;

        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 100;
        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 90;

        if (props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(g.Address) &&
                !g.Address.Equals(IPAddress.Any)))
            score += 50;

        if (ni.Description.ToLowerInvariant().Contains("virtual")) score -= 30;

        return score;
    }
}
