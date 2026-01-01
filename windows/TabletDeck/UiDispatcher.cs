using System;
using System.Windows.Forms;

namespace TabletDeck;

/// <summary>
/// Prosty dispatcher na wątek UI (STA) – potrzebne dla części API (WinRT / okna / SendMessage).
/// </summary>
internal static class UiDispatcher
{
    private static Control? _control;

    public static void InitOnUiThread()
    {
        // Wywołaj z wątku UI (STA) w Main(), zanim ruszy serwer.
        _control = new Control();
        _control.CreateControl(); // tworzy uchwyt; BeginInvoke będzie działał po starcie message-loop
    }

    public static void Post(Action action)
    {
        var c = _control;
        if (c is { IsHandleCreated: true })
        {
            try { c.BeginInvoke(action); }
            catch { action(); } // awaryjnie
        }
        else
        {
            action();
        }
    }
}
