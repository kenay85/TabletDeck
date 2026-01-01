using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace TabletDeck;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // language (safe fallback)
        try
        {
            var cfg = AppConfigStore.LoadOrCreate();
            Localization.SetLanguage(cfg.Language);
        }
        catch
        {
            Localization.SetLanguage(Localization.DefaultLanguage);
        }


            UiDispatcher.InitOnUiThread();

        var settings = ServerSettingsStore.LoadOrCreate(defaultPort: 8787);
        var ip = GetLocalIpv4OrLoopback();
        var token = settings.Token;
        var port = settings.Port;

        var wsUrl = $"ws://{ip}:{port}/ws?token={token}";

        var server = new WsServer(port, token);
        server.Start();
        server.SetLanguage(Localization.LanguageCode);


        Application.Run(new TrayAppContext(server, wsUrl));
    }

    private static string GetLocalIpv4OrLoopback()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&          // VPN / tunnel
                ni.NetworkInterfaceType != NetworkInterfaceType.Ppp &&             // czêste w VPN
                !IsLikelyVpnAdapter(ni))
            .SelectMany(ni =>
            {
                var props = ni.GetIPProperties();
                return props.UnicastAddresses
                    .Where(ua =>
                        ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ua.Address) &&
                        !IsLinkLocal169(ua.Address))
                    .Select(ua => new
                    {
                        Ip = ua.Address.ToString(),
                        Score = ScoreInterface(ni, props)
                    });
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        return candidates.FirstOrDefault()?.Ip ?? "127.0.0.1";
    }

    private static bool IsLinkLocal169(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 169 && b[1] == 254;
    }

    private static bool IsLikelyVpnAdapter(NetworkInterface ni)
    {
        var s = $"{ni.Name} {ni.Description}".ToLowerInvariant();

        // typowe VPN/TUN/TAP/WireGuard/OpenVPN/Cisco itp.
        return s.Contains("vpn")
            || s.Contains("wireguard")
            || s.Contains("wintun")
            || s.Contains("openvpn")
            || s.Contains("tap")
            || s.Contains("tun")
            || s.Contains("tailscale")
            || s.Contains("zerotier")
            || s.Contains("cisco")
            || s.Contains("fortinet")
            || s.Contains("pulse secure");
    }

    private static int ScoreInterface(NetworkInterface ni, IPInterfaceProperties props)
    {
        var score = 0;

        // Preferuj typowe “LAN”
        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 100;
        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 90;

        // Preferuj interfejsy z bram¹ (czêsto “prawdziwe” LAN)
        if (props.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(g.Address) &&
                !g.Address.Equals(IPAddress.Any)))
            score += 50;

        // Preferuj nie-wirtualne
        if (ni.Description.ToLowerInvariant().Contains("virtual")) score -= 30;

        return score;
    }
}