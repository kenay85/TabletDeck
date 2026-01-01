using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace TabletDeck;

internal sealed record AppCandidate(string DisplayName, string Target);

internal static class AppIndex
{
    private static readonly object Gate = new();
    private static List<AppCandidate>? _cache;

    public static IReadOnlyList<AppCandidate> GetOrBuild()
    {
        lock (Gate)
        {
            _cache ??= BuildInternal();
            return _cache;
        }
    }

    public static void Invalidate()
    {
        lock (Gate) _cache = null;
    }

    public static IEnumerable<AppCandidate> Search(string query, int take = 30)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<AppCandidate>();
        query = query.Trim();

        var all = GetOrBuild();

        var starts = all.Where(a => a.DisplayName.StartsWith(query, StringComparison.CurrentCultureIgnoreCase));
        var contains = all.Where(a => a.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        return starts.Concat(contains).Distinct().Take(take);
    }

    private static List<AppCandidate> BuildInternal()
    {
        var list = new List<AppCandidate>();

        list.AddRange(ReadStartMenuShortcuts());
        list.AddRange(ReadAppPathsRegistry());

        return list
            .Where(a => !string.IsNullOrWhiteSpace(a.DisplayName) && !string.IsNullOrWhiteSpace(a.Target))
            .Select(a => new AppCandidate(a.DisplayName.Trim(), a.Target.Trim()))
            .GroupBy(a => (a.DisplayName, a.Target), new TupleKeyComparer())
            .Select(g => g.First())
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private sealed class TupleKeyComparer : IEqualityComparer<(string DisplayName, string Target)>
    {
        public bool Equals((string DisplayName, string Target) x, (string DisplayName, string Target) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.DisplayName, y.DisplayName)
               && StringComparer.OrdinalIgnoreCase.Equals(x.Target, y.Target);

        public int GetHashCode((string DisplayName, string Target) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.DisplayName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Target));
    }

    private static List<AppCandidate> ReadStartMenuShortcuts()
    {
        var results = new List<AppCandidate>();

        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        }
        .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var root in roots)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                        f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".appref-ms", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                continue;
            }

            foreach (var f in files)
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    results.Add(new AppCandidate(name, f));
                }
                catch { }
            }
        }

        return results;
    }

    private static List<AppCandidate> ReadAppPathsRegistry()
    {
        var results = new List<AppCandidate>();

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            RegistryKey? key = null;

            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (key is null) continue;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub is null) continue;

                        var path = (sub.GetValue(null) as string)?.Trim();
                        if (string.IsNullOrWhiteSpace(path)) continue;
                        if (!File.Exists(path)) continue;

                        var display = Path.GetFileNameWithoutExtension(subName);
                        if (string.IsNullOrWhiteSpace(display)) display = subName;

                        results.Add(new AppCandidate(display, path));
                    }
                    catch { }
                }
            }
            catch
            {
                // ignorujemy problem z rejestrem
            }
            finally
            {
                key?.Dispose();
            }
        }

        return results;
    }
}
