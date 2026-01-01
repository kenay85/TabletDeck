using System;
using Microsoft.Win32;

namespace TabletDeck;

/// <summary>
/// Hooks Windows system events to gracefully shut down background services.
/// </summary>
internal static class SystemEventsHook
{
    private static readonly object Sync = new();
    private static bool _attached;
    private static bool _exitRequested;
    private static Action? _onExit;

    public static void Attach(Action onExit)
    {
        if (onExit is null) throw new ArgumentNullException(nameof(onExit));

        lock (Sync)
        {
            if (_attached) return;
            _attached = true;
            _onExit = onExit;

            SystemEvents.SessionEnding += OnSessionEnding;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
    }

    private static void OnSessionEnding(object? sender, SessionEndingEventArgs e) => RequestExitOnce();

    private static void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
            RequestExitOnce();
    }

    private static void OnProcessExit(object? sender, EventArgs e) => RequestExitOnce();

    private static void RequestExitOnce()
    {
        Action? cb;
        lock (Sync)
        {
            if (_exitRequested) return;
            _exitRequested = true;
            cb = _onExit;
        }

        try { cb?.Invoke(); } catch { }
    }
}
