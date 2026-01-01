using System;
using System.Runtime.InteropServices;

namespace TabletDeck;

public static class WinAppCommandMedia
{
    private const int WM_APPCOMMAND = 0x0319;
    private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
    private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
    private const int APPCOMMAND_MEDIA_STOP = 13;
    private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

    // Timeout flags
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    public static bool TrySend(string cmd)
    {
        cmd = (cmd ?? "").Trim().ToLowerInvariant();

        int appCmd = cmd switch
        {
            "playpause" => APPCOMMAND_MEDIA_PLAY_PAUSE,
            "stop" => APPCOMMAND_MEDIA_STOP,
            "next" => APPCOMMAND_MEDIA_NEXTTRACK,
            "prev" => APPCOMMAND_MEDIA_PREVIOUSTRACK,
            _ => 0
        };

        if (appCmd == 0) return false;

        // Wysyłamy do okna na wierzchu (największa szansa, że trafi do przeglądarki / playera).
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        // lParam: appCmd << 16
        IntPtr lParam = (IntPtr)(appCmd << 16);
        SendMessageTimeout(hwnd, WM_APPCOMMAND, hwnd, lParam, SMTO_ABORTIFHUNG, 200, out _);
        return true;
    }
}
