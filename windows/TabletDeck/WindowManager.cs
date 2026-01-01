// ===============================
// File: /TabletDeck/WindowManager.cs
// ===============================
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TabletDeck;

/// <summary>
/// Minimalne przywracanie/fokusowanie okna procesu (StreamDeck-style).
/// </summary>
internal static class WindowManager
{
    public static bool TryFocusProcess(string processName)
    {
        var proc = Process.GetProcessesByName(processName)
            .OrderByDescending(p => p.StartTime)
            .FirstOrDefault();

        if (proc is null)
            return false;

        return TryFocusProcessId(proc.Id);
    }

    public static bool TryFocusProcessId(int processId)
    {
        nint found = 0;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != processId)
                return true;

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            found = hWnd;
            return false;
        }, 0);

        if (found == 0)
            return false;

        ShowWindow(found, SW_RESTORE);
        SetForegroundWindow(found);
        return true;
    }

    private static string GetWindowTitle(nint hWnd)
    {
        var len = GetWindowTextLength(hWnd);
        if (len <= 0)
            return string.Empty;

        var sb = new StringBuilder(len + 1);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
