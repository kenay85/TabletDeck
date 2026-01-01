using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TabletDeck
{
    internal static class WinSendInputMedia
    {
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-keybd_event
        [DllImport("user32.dll", SetLastError = false)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private static readonly Dictionary<string, byte> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["playpause"] = 0xB3, // VK_MEDIA_PLAY_PAUSE
            ["next"] = 0xB0, // VK_MEDIA_NEXT_TRACK
            ["prev"] = 0xB1, // VK_MEDIA_PREV_TRACK
            ["stop"] = 0xB2, // VK_MEDIA_STOP

            // fallback dla volume/mute (gdyby CoreAudio wywaliło)
            ["volup"] = 0xAF, // VK_VOLUME_UP
            ["voldown"] = 0xAE, // VK_VOLUME_DOWN
            ["mute"] = 0xAD, // VK_VOLUME_MUTE
        };

        public static bool TrySend(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return false;

            cmd = cmd.Trim();

            if (!Map.TryGetValue(cmd, out var vk))
                return false;

            try
            {
                // key down + key up
                keybd_event(vk, 0, 0, UIntPtr.Zero);
                keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                Log.Info($"[MEDIA] keybd_event vk=0x{vk:X2} cmd='{cmd}'");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn($"[MEDIA] keybd_event failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
    }
}
