using System;
using System.Diagnostics;
using System.IO;

namespace TabletDeck;

internal static class Log
{
    private static readonly object LockObj = new();
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "tabletdeck.log");

    public static void Info(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        Debug.WriteLine(line);

        lock (LockObj)
        {
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }
    }
    public static void Warn(string msg)
    { 
        Info(msg);
    }
    public static string PathToLogFile() => FilePath;
}