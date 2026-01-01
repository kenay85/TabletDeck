// File: TabletDeck/WsServer.cs
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace TabletDeck;

/// <summary>
/// WebSocket server + broadcast metryk + akcje (launch/run/media) + upload plików.
/// </summary>
public sealed class WsServer : IDisposable
{
    private readonly int _port;
    private readonly string _token;
    private string _language = Localization.DefaultLanguage;

    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(1));

    // Windows -> Android file push (tray menu)
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pcFileDecisions =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ObsActions _obsActions = new();

    private WebApplication? _app;
    private Task? _runTask;
    private bool _disposed;
    private int _stopping;
    private Guid? _lastClientId;

    private static long _lastVolTick;

    public WsServer(int port, string token)
    {
        _port = port;
        _token = token;
    }

    public void Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls($"http://0.0.0.0:{_port}");

        _app = builder.Build();
        _app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });

        _app.MapGet("/", () => Results.Text("TabletDeck PC OK"));

        // Upload plików: PUT /upload?token=...  +  header: X-Filename: name.ext
        _app.MapPut("/upload", async (HttpContext ctx) =>
        {
            var token = ctx.Request.Query["token"].ToString();
            if (!string.Equals(token, _token, StringComparison.Ordinal))
                return Results.Unauthorized();

            var filename = ctx.Request.Headers["X-Filename"].ToString();
            if (string.IsNullOrWhiteSpace(filename))
                filename = "upload.bin";

            filename = SanitizeFileName(filename);

            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "TabletDeckUploads"
            );
            Directory.CreateDirectory(downloads);

            var destPath = GetUniquePath(Path.Combine(downloads, filename));

            await using (var fs = File.Create(destPath))
            {
                await ctx.Request.Body.CopyToAsync(fs, _cts.Token);
            }

            return Results.Json(new { ok = true, file = Path.GetFileName(destPath) });
        });

        // WebSocket: /ws?token=...
        _app.Map("/ws", async ctx =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            var token = ctx.Request.Query["token"].ToString();
            if (!string.Equals(token, _token, StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = 401;
                return;
            }

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();

            var id = Guid.NewGuid();
            _clients[id] = ws;
            _lastClientId = id;

            // hello -> actions + layout
            var cfg = AppConfigStore.LoadOrCreate();
            var active = cfg.Profiles.First(p => p.Id == cfg.ActiveProfileId);

            await SendJsonAsync(ws, new
            {
                type = "hello",
                lang = _language,
                pcName = Environment.MachineName,
                actions = cfg.Catalog.Select(a =>
                {
                    var icon = IconExtractor.GetIconPngBase64(a.Id);
                    return new { id = a.Id, label = a.Label, icon = icon, iconPng = icon };
                }).ToArray(),
                layout = new
                {
                    rows = active.Rows,
                    cols = active.Cols,
                    cells = active.Cells,
                    tileHeightDp = active.TileHeightDp,
                    iconSizeDp = active.IconSizeDp
                }
            }, _cts.Token);

            try
            {
                await ReceiveLoopAsync(id, ws, _cts.Token);
            }
            finally
            {
                _clients.TryRemove(id, out _);
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
            }
        });

        _runTask = _app.RunAsync(_cts.Token);
        _ = Task.Run(BroadcastMetricsLoopAsync, _cts.Token);
    }

    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopping, 1) != 0)
            return;

        if (_disposed) return;
        _disposed = true;

        try { _cts.Cancel(); } catch { }

        // Close websocket clients to avoid hanging process on exit.
        foreach (var kv in _clients.ToArray())
        {
            var ws = kv.Value;
            try
            {
                if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", timeout.Token)
                        .ConfigureAwait(false);
                }
            }
            catch { }

            try { ws.Abort(); } catch { }
            try { ws.Dispose(); } catch { }

            _clients.TryRemove(kv.Key, out _);
        }

        try { _timer.Dispose(); } catch { }

        try
        {
            if (_app != null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _app.StopAsync(timeout.Token).ConfigureAwait(false);
            }
        }
        catch { }

        try
        {
            if (_app is IAsyncDisposable ad) await ad.DisposeAsync().ConfigureAwait(false);
            else if (_app is IDisposable d) d.Dispose();
        }
        catch { }

        _app = null;
        _runTask = null;
    }

    private async Task ReceiveLoopAsync(Guid clientId, WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[32 * 1024];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var res = await ws.ReceiveAsync(buffer, ct);
            if (res.MessageType == WebSocketMessageType.Close) break;

            var json = Encoding.UTF8.GetString(buffer, 0, res.Count);
            Log.Info($"[WS<-ANDROID] {json}");

            HandleIncoming(clientId, json);
        }
    }

    private void HandleIncoming(Guid clientId, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeEl))
            {
                var type = typeEl.GetString() ?? "";

                if (string.Equals(type, "pc_file_accept", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "pc_file_reject", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(id) && _pcFileDecisions.TryRemove(id, out var tcs))
                            tcs.TrySetResult(string.Equals(type, "pc_file_accept", StringComparison.OrdinalIgnoreCase));
                    }
                    return;
                }
            }
        }
        catch
        {
            // ignore and fall back to old handler
        }

        TryHandleIncoming(json);
    }

    private static void TryHandleIncoming(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeEl)) return;
            if (!string.Equals(typeEl.GetString(), "action", StringComparison.OrdinalIgnoreCase)) return;

            if (!root.TryGetProperty("actionId", out _)) return;

            var actionId = root.GetProperty("actionId").GetString() ?? "";
            Console.WriteLine($"[WS] actionId = '{actionId}'  userInteractive={Environment.UserInteractive}");
            HandleAction(actionId);
        }
        catch
        {
            // ignore
        }
    }

    private static void HandleAction(string actionId)
    {
        Log.Info($"[ACTION] actionId='{actionId}' userInteractive={Environment.UserInteractive}");
        if (string.IsNullOrWhiteSpace(actionId))
            return;

        if (actionId.StartsWith("obs:", StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try { await _obsActions.TryHandleAsync(actionId, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) { Log.Warn($"[OBS] Background handle failed: {ex.Message}"); }
            });
            return;
        }

        if (actionId.StartsWith("media:", StringComparison.OrdinalIgnoreCase))
        {
            var cmd = actionId["media:".Length..].Trim();
            Log.Info($"[MEDIA] cmd='{cmd}'");
            if (cmd is "volup" or "voldown")
            {
                var now = Environment.TickCount64;
                if (now - _lastVolTick < 60) return;
                _lastVolTick = now;
            }

            UiDispatcher.Post(() => global::TabletDeck.MediaKeys.Send(cmd));
            return;
        }

        if (TryHandleRunAction(actionId))
            return;

        TryHandleLaunchAction(actionId);
    }

    private static bool TryHandleRunAction(string actionId)
    {
        var prefix = actionId.StartsWith("runOrFocus:", StringComparison.OrdinalIgnoreCase)
            ? "runOrFocus:"
            : actionId.StartsWith("run:", StringComparison.OrdinalIgnoreCase)
                ? "run:"
                : null;

        if (prefix is null)
            return false;

        var raw = actionId[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var (target, args) = ParseTargetAndArgs(raw);
        if (string.IsNullOrWhiteSpace(target))
            return true;

        var shouldFocus = prefix.Equals("runOrFocus:", StringComparison.OrdinalIgnoreCase);
        if (shouldFocus)
        {
            var processName = TryDeriveProcessName(target);
            if (!string.IsNullOrWhiteSpace(processName) && WindowManager.TryFocusProcess(processName))
                return true;
        }

        try { StartShellExecute(target, args); } catch { }
        return true;
    }

    private static void TryHandleLaunchAction(string actionId)
    {
        var prefix = actionId.StartsWith("launchOrFocus:", StringComparison.OrdinalIgnoreCase)
            ? "launchOrFocus:"
            : actionId.StartsWith("launch:", StringComparison.OrdinalIgnoreCase)
                ? "launch:"
                : null;

        if (prefix is null)
            return;

        var app = actionId[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(app))
            return;

        var (processName, exe) = app switch
        {
            "notepad" => ("notepad", "notepad.exe"),
            "calc" => ("calc", "calc.exe"),
            _ => (app, app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? app : $"{app}.exe")
        };

        var shouldFocus = prefix.Equals("launchOrFocus:", StringComparison.OrdinalIgnoreCase);
        if (shouldFocus && WindowManager.TryFocusProcess(processName))
            return;

        try { Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true }); } catch { }
    }

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

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s[1..^1].Trim();
        return s;
    }

    private static string? TryDeriveProcessName(string target)
    {
        try
        {
            if (target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(target);

            if (File.Exists(target) && string.Equals(Path.GetExtension(target), ".exe", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(target);
        }
        catch { }

        return null;
    }

    private static void StartShellExecute(string target, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = target,
            Arguments = args ?? "",
            UseShellExecute = true
        };

        try
        {
            if (File.Exists(target))
            {
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(dir))
                    psi.WorkingDirectory = dir;
            }
        }
        catch { }

        Process.Start(psi);
    }

    private async Task BroadcastMetricsLoopAsync()
    {
        var sampler = new MetricsSampler();

        while (await _timer.WaitForNextTickAsync(_cts.Token))
        {
            MetricsSnapshot m;
            try { m = sampler.Sample(); }
            catch (Exception ex)
            {
                Log.Info($"[WS] Metrics sampler failed: {ex}");
                continue;
            }

            var payload = new
            {
                type = "metrics",
                cpuPct = m.CpuPct,
                cpuTempC = m.CpuTempC,
                gpuName = m.GpuName,
                gpuPct = m.GpuPct,
                gpuTempC = m.GpuTempC,
                gpuMemUsedMb = m.GpuMemUsedMb,
                gpuMemTotalMb = m.GpuMemTotalMb,
                ramUsedMb = m.RamUsedMb,
                ramTotalMb = m.RamTotalMb,
                diskFreeGb = m.DiskFreeGb,
                disks = m.Disks.Select(d => new { name = d.Name, totalGb = d.TotalGb, freeGb = d.FreeGb })
            };

            foreach (var kv in _clients.ToArray())
            {
                var ws = kv.Value;
                if (ws.State != WebSocketState.Open) continue;
                try { await SendJsonAsync(ws, payload, _cts.Token); } catch { }
            }
        }
    }

    public void SetLanguage(string? lang)
    {
        _language = Localization.NormalizeLanguageCode(lang);

        if (_disposed) return;
        BroadcastLanguageNow();
    }

    public void BroadcastLanguageNow()
    {
        if (_disposed) return;
        _ = Task.Run(() => BroadcastLanguageToClientsAsync(_cts.Token));
    }

    private async Task BroadcastLanguageToClientsAsync(CancellationToken ct)
    {
        if (_clients.IsEmpty) return;

        var payload = new { type = "lang", lang = _language };

        foreach (var ws in _clients.Values)
        {
            if (ws.State != WebSocketState.Open) continue;
            try { await SendJsonAsync(ws, payload, ct); } catch { }
        }
    }

    public void BroadcastLayoutNow()
    {
        if (_disposed) return;
        _ = Task.Run(() => BroadcastLayoutToClientsAsync(_cts.Token));
    }

    private async Task BroadcastLayoutToClientsAsync(CancellationToken ct)
    {
        if (_clients.IsEmpty) return;

        AppConfig cfg;
        try { cfg = AppConfigStore.LoadOrCreate(); }
        catch { return; }

        ProfileLayout active;
        try { active = cfg.Profiles.First(p => p.Id == cfg.ActiveProfileId); }
        catch { return; }

        var payload = new
        {
            type = "layout",
            layout = new
            {
                rows = active.Rows,
                cols = active.Cols,
                cells = active.Cells,
                tileHeightDp = active.TileHeightDp,
                iconSizeDp = active.IconSizeDp
            }
        };

        foreach (var ws in _clients.Values)
        {
            if (ws.State != WebSocketState.Open) continue;
            try { await SendJsonAsync(ws, payload, ct); } catch { }
        }
    }

    // =========================================================
    // Windows -> Android: file push (tray menu)
    // =========================================================

    public bool TryGetAnyConnectedClient(out Guid clientId)
    {
        foreach (var kv in _clients)
        {
            if (kv.Value.State == WebSocketState.Open)
            {
                clientId = kv.Key;
                return true;
            }
        }

        clientId = default;
        return false;
    }

    public async Task SendFileToClientAsync(Guid clientId, string filePath, CancellationToken ct)
    {
        if (!_clients.TryGetValue(clientId, out var ws) || ws.State != WebSocketState.Open)
            throw new InvalidOperationException("Android client is not connected.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found.", filePath);

        var fi = new FileInfo(filePath);
        var transferId = Guid.NewGuid().ToString("N");
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pcFileDecisions[transferId] = decision;

        await SendJsonAsync(ws, new
        {
            type = "pc_file_offer",
            id = transferId,
            name = SanitizeFileName(Path.GetFileName(filePath)),
            size = fi.Length
        }, ct).ConfigureAwait(false);

        bool accepted;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            accepted = await decision.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch
        {
            accepted = false;
        }
        finally
        {
            _pcFileDecisions.TryRemove(transferId, out _);
        }

        if (!accepted)
            return;

        await SendJsonAsync(ws, new { type = "pc_file_begin", id = transferId }, ct).ConfigureAwait(false);

        const int chunkSize = 12 * 1024; // base64-safe (<32KB payloads)
        var buffer = new byte[chunkSize];
        var seq = 0;

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, useAsync: true);
        while (true)
        {
            var read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read <= 0) break;

            var b64 = Convert.ToBase64String(buffer, 0, read);
            await SendJsonAsync(ws, new { type = "pc_file_chunk", id = transferId, seq, data = b64 }, ct).ConfigureAwait(false);
            seq++;
        }

        await SendJsonAsync(ws, new { type = "pc_file_end", id = transferId }, ct).ConfigureAwait(false);
    }

    private static Task SendJsonAsync(WebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        return ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static string SanitizeFileName(string name)
    {
        name = name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Replace("..", "_");
        if (string.IsNullOrWhiteSpace(name))
            name = "upload.bin";

        return name;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var file = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        for (var i = 1; i < 10_000; i++)
        {
            var cand = Path.Combine(dir, $"{file} ({i}){ext}");
            if (!File.Exists(cand))
                return cand;
        }

        return Path.Combine(dir, $"{file}_{Guid.NewGuid():N}{ext}");
    }

    public void Dispose()
    {
        try { Stop(); } catch { }
        try { _cts.Dispose(); } catch { }
    }

    // (zostawiłem Twoją klasę MediaKeys na końcu jeśli ją miałeś w tej wersji pliku)
    private static class MediaKeys
    {
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
        private const ushort VK_MEDIA_PREV_TRACK = 0xB1;
        private const ushort VK_MEDIA_STOP = 0xB2;
        private const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;

        private const ushort VK_VOLUME_MUTE = 0xAD;
        private const ushort VK_VOLUME_DOWN = 0xAE;
        private const ushort VK_VOLUME_UP = 0xAF;

        internal static void Send(string cmd)
        {
            ushort vk = cmd.ToLowerInvariant() switch
            {
                "next" => VK_MEDIA_NEXT_TRACK,
                "prev" => VK_MEDIA_PREV_TRACK,
                "stop" => VK_MEDIA_STOP,
                "playpause" => VK_MEDIA_PLAY_PAUSE,
                "mute" => VK_VOLUME_MUTE,
                "voldown" => VK_VOLUME_DOWN,
                "volup" => VK_VOLUME_UP,
                _ => 0
            };

            if (vk == 0) return;

            Key(vk, false);
            Key(vk, true);
        }

        private static void Key(ushort vk, bool up)
        {
            INPUT input = new()
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 }
                }
            };

            _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}
