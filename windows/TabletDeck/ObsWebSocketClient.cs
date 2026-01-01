using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TabletDeck;

internal sealed class ObsWebSocketClient : IAsyncDisposable
{
    private readonly object _gate = new();
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _recvLoop;

    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> _pending = new();

    private Uri? _uri;
    private string? _password;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task EnsureConnectedAsync(ObsSettings s, CancellationToken ct)
    {
        var uri = BuildUri(s);
        var password = s.Password ?? "";

        lock (_gate)
        {
            if (_uri is not null &&
                _password is not null &&
                _uri == uri &&
                _password == password &&
                IsConnected)
            {
                return;
            }
        }

        await DisconnectAsync().ConfigureAwait(false);
        await ConnectAsync(uri, password, ct).ConfigureAwait(false);
    }

    public async Task<JsonElement> SendRequestAsync(string requestType, object? requestData, CancellationToken ct)
    {
        if (_ws is null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("OBS websocket is not connected.");

        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
            _pending[requestId] = tcs;

        var payload = new
        {
            op = 6,
            d = new
            {
                requestType,
                requestId,
                requestData
            }
        };

        await SendJsonAsync(payload, ct).ConfigureAwait(false);

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        return await tcs.Task.ConfigureAwait(false);
    }

    private static Uri BuildUri(ObsSettings s)
    {
        var host = (s.Host ?? "127.0.0.1").Trim();
        if (host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri($"{host.TrimEnd('/')}:{s.Port}/");
        }

        return new Uri($"ws://{host}:{s.Port}/");
    }

    private async Task ConnectAsync(Uri uri, string password, CancellationToken ct)
    {
        Log.Info($"[OBS] Connecting to {uri} ...");

        var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await ws.ConnectAsync(uri, cts.Token).ConfigureAwait(false);

        // Expect Hello (op=0)
        var hello = await ReceiveJsonAsync(ws, cts.Token).ConfigureAwait(false);
        var helloOp = hello.GetProperty("op").GetInt32();
        if (helloOp != 0)
            throw new InvalidOperationException($"OBS: expected Hello(op=0), got op={helloOp}");

        var d = hello.GetProperty("d");
        var rpcVersion = d.GetProperty("rpcVersion").GetInt32();

        string? auth = null;
        if (d.TryGetProperty("authentication", out var authObj))
        {
            var salt = authObj.GetProperty("salt").GetString() ?? "";
            var challenge = authObj.GetProperty("challenge").GetString() ?? "";
            auth = ComputeAuth(password, salt, challenge);
        }

        // Identify (op=1)
        var identify = new
        {
            op = 1,
            d = new
            {
                rpcVersion = rpcVersion,
                authentication = auth,
                eventSubscriptions = 0
            }
        };

        await SendJsonAsync(ws, identify, cts.Token).ConfigureAwait(false);

        // Expect Identified (op=2)
        var identified = await ReceiveJsonAsync(ws, cts.Token).ConfigureAwait(false);
        var idOp = identified.GetProperty("op").GetInt32();
        if (idOp != 2)
        {
            // OBS sometimes sends op=3 (Reidentify) or closes; handle as error for now
            throw new InvalidOperationException($"OBS: expected Identified(op=2), got op={idOp} payload={identified}");
        }

        lock (_gate)
        {
            _uri = uri;
            _password = password;
            _ws = ws;
            _cts = cts;
            _recvLoop = Task.Run(() => ReceiveLoopAsync(ws, cts.Token));
        }

        Log.Info("[OBS] Connected & Identified.");
    }

    private static string ComputeAuth(string password, string salt, string challenge)
    {
        // secret = base64(sha256(password + salt))
        // auth   = base64(sha256(secret + challenge))
        var secret = Sha256Base64(password + salt);
        return Sha256Base64(secret + challenge);

        static string Sha256Base64(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var msg = await ReceiveJsonAsync(ws, ct).ConfigureAwait(false);
                var op = msg.GetProperty("op").GetInt32();

                if (op == 7)
                {
                    var d = msg.GetProperty("d");
                    var requestId = d.GetProperty("requestId").GetString() ?? "";
                    TaskCompletionSource<JsonElement>? tcs;

                    lock (_gate)
                    {
                        _pending.TryGetValue(requestId, out tcs);
                        if (tcs is not null) _pending.Remove(requestId);
                    }

                    if (tcs is not null)
                        tcs.TrySetResult(d);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[OBS] Receive loop stopped: {ex.Message}");
            FailAllPending(ex);
        }
    }

    private void FailAllPending(Exception ex)
    {
        List<TaskCompletionSource<JsonElement>> all;
        lock (_gate)
        {
            all = _pending.Values.ToList();
            _pending.Clear();
        }

        foreach (var tcs in all)
            tcs.TrySetException(ex);
    }

    private async Task SendJsonAsync(object payload, CancellationToken ct)
    {
        var ws = _ws ?? throw new InvalidOperationException("Not connected.");
        await SendJsonAsync(ws, payload, ct).ConfigureAwait(false);
    }

    private static async Task SendJsonAsync(ClientWebSocket ws, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
    }

    private static async Task<JsonElement> ReceiveJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        using var ms = new MemoryStream();

        while (true)
        {
            var seg = new ArraySegment<byte>(buffer);
            var res = await ws.ReceiveAsync(seg, ct).ConfigureAwait(false);

            if (res.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("OBS websocket closed.");

            ms.Write(buffer, 0, res.Count);
            if (res.EndOfMessage) break;
        }

        var doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    public async Task DisconnectAsync()
    {
        ClientWebSocket? ws;
        CancellationTokenSource? cts;

        lock (_gate)
        {
            ws = _ws;
            cts = _cts;
            _ws = null;
            _cts = null;
            _recvLoop = null;
            _uri = null;
            _password = null;
        }

        try { cts?.Cancel(); } catch { }

        if (ws is not null)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).ConfigureAwait(false);
            }
            catch { }
            try { ws.Dispose(); } catch { }
        }

        FailAllPending(new OperationCanceledException("Disconnected."));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}