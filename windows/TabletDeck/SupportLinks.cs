using System.Diagnostics;

namespace TabletDeck;

internal static class SupportLinks
{
    public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/kenay";

    public static void OpenBuyMeACoffee()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BuyMeACoffeeUrl) { UseShellExecute = true });
        }
        catch
        {
            // Avoid crashing tray app if shell start fails.
        }
    }
}
