using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;


namespace TabletDeck;

public static class IconExtractor
{
    private const int IconSizePx = 64;

    private static readonly ConcurrentDictionary<string, string?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string? GetIconPngBase64(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return null;

        return Cache.GetOrAdd(actionId, static id => TryExtractBase64Png(id));
    }

    private static string? TryExtractBase64Png(string actionId)
    {
        if (actionId.StartsWith("media:", StringComparison.OrdinalIgnoreCase))
            return null;

        var exePath = TryResolveExePath(actionId);
        if (exePath is null)
            return null;
        if (exePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = TryResolveShortcutTarget(exePath);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                exePath = resolved;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
                return null;

            using var bmp = icon.ToBitmap();
            using var resized = ResizeToSquare(bmp, IconSizePx);

            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveExePath(string actionId)
    {
        var raw = StripPrefix(actionId, "runOrFocus:") ?? StripPrefix(actionId, "run:");
        if (raw != null)
        {
            var (target, _) = ParseTargetAndArgs(raw);
            return ResolveExe(target);
        }

        raw = StripPrefix(actionId, "launchOrFocus:") ?? StripPrefix(actionId, "launch:");
        if (raw != null)
        {
            var app = raw.Trim();
            if (app.Length == 0)
                return null;

            var exe = app switch
            {
                "notepad" => "notepad.exe",
                "calc" => "calc.exe",
                _ => app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? app : $"{app}.exe"
            };

            return ResolveExe(exe);
        }

        return ResolveExe(actionId);
    }

    private static string? StripPrefix(string s, string prefix)
        => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? s[prefix.Length..].Trim() : null;

    private static (string Target, string Args) ParseTargetAndArgs(string raw)
    {
        raw = Environment.ExpandEnvironmentVariables(raw.Trim());
        if (raw.Length == 0)
            return ("", "");

        var sepIdx = raw.IndexOf("||", StringComparison.Ordinal);
        if (sepIdx >= 0)
        {
            var left = raw[..sepIdx].Trim();
            var right = raw[(sepIdx + 2)..].Trim();
            var split = TrySplitByKnownExtension(raw);
            if (split is not null)
                return split.Value;

            return (Unquote(left), right);
        }

        if (raw[0] == '"')
        {
            var end = raw.IndexOf('"', 1);
            if (end > 0)
            {
                var target = raw.Substring(1, end - 1).Trim();
                var args = raw[(end + 1)..].Trim();
                return (target, args);
            }
        }

        return (Unquote(raw), "");
    }

    private static (string Target, string Args)? TrySplitByKnownExtension(string raw)
    {
        foreach (var ext in new[] { ".exe", ".lnk", ".bat", ".cmd", ".appref-ms" })
        {
            var idx = raw.IndexOf(ext, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var end = idx + ext.Length;
            if (end <= 0 || end >= raw.Length) continue;
            if (!char.IsWhiteSpace(raw[end])) continue;

            var target = Unquote(raw[..end].Trim());
            var args = raw[end..].Trim();
            if (target.Length == 0) continue;

            var expanded = Environment.ExpandEnvironmentVariables(target);
            if (File.Exists(expanded))
                return (expanded, args);

            return (target, args);
        }

        return null;
    }

    private static string? TryResolveShortcutTarget(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;

            dynamic? shell = null;
            dynamic? shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell is null) return null;

                shortcut = shell.CreateShortcut(lnkPath);
                if (shortcut is null) return null;

                string targetPath = shortcut.TargetPath;
                return string.IsNullOrWhiteSpace(targetPath) ? null : targetPath;
            }
            finally
            {
                if (shortcut is not null) Marshal.FinalReleaseComObject(shortcut);
                if (shell is not null) Marshal.FinalReleaseComObject(shell);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s[1..^1].Trim();

        return s;
    }

    private static string? ResolveExe(string? maybe)
    {
        if (string.IsNullOrWhiteSpace(maybe))
            return null;

        var target = Environment.ExpandEnvironmentVariables(maybe.Trim().Trim('"'));

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            return null;

        try
        {
            if (File.Exists(target))
                return target;
        }
        catch { }

        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            target += ".exe";

        var candidates = new List<string>();

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator).Where(d => !string.IsNullOrWhiteSpace(d)))
            candidates.Add(Path.Combine(dir.Trim(), target));

        candidates.Add(Path.Combine(Environment.SystemDirectory, target));

        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(winDir))
        {
            candidates.Add(Path.Combine(winDir, target));
            candidates.Add(Path.Combine(winDir, "System32", target));
        }

        foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(c))
                    return c;
            }
            catch { }
        }

        return null;
    }

    private static Bitmap ResizeToSquare(Bitmap src, int size)
    {
        var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(dst);
        g.Clear(Color.Transparent);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        var scale = Math.Min((float)size / src.Width, (float)size / src.Height);
        var w = (int)(src.Width * scale);
        var h = (int)(src.Height * scale);
        var x = (size - w) / 2;
        var y = (size - h) / 2;

        g.DrawImage(src, x, y, w, h);
        return dst;
    }
}