namespace TabletDeck;

internal sealed class ObsActions
{
    private readonly ObsWebSocketClient _client = new();

    public async Task<bool> TryHandleAsync(string actionId, CancellationToken ct)
    {
        var s = ObsSettingsStore.Load();

        if (!s.Enabled)
        {
            Log.Info("[OBS] Action requested but OBS integration is disabled.");
            return false;
        }

        Log.Info($"[OBS] Action: {actionId} -> {s.Host}:{s.Port}");

        try
        {
            await _client.EnsureConnectedAsync(s, ct).ConfigureAwait(false);

            switch (actionId)
            {
                case "obs:stream:toggle":
                    await _client.SendRequestAsync("ToggleStream", null, ct).ConfigureAwait(false);
                    break;

                case "obs:record:toggle":
                    await _client.SendRequestAsync("ToggleRecord", null, ct).ConfigureAwait(false);
                    break;

                case "obs:replay:save":
                    await _client.SendRequestAsync("SaveReplayBuffer", null, ct).ConfigureAwait(false);
                    break;

                case "obs:scene:prev":
                    await ChangeSceneAsync(-1, ct).ConfigureAwait(false);
                    break;

                case "obs:scene:next":
                    await ChangeSceneAsync(+1, ct).ConfigureAwait(false);
                    break;

                case "obs:audio:mic:toggleMute":
                    await ToggleSpecialInputMuteAsync("mic1", ct).ConfigureAwait(false);
                    break;

                case "obs:audio:desktop:toggleMute":
                    await ToggleSpecialInputMuteAsync("desktop1", ct).ConfigureAwait(false);
                    break;

                default:
                    Log.Info($"[OBS] Unknown actionId: {actionId}");
                    return false;
            }

            Log.Info($"[OBS] OK: {actionId}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[OBS] FAIL {actionId}: {ex}");
            return false;
        }
    }

    private async Task ChangeSceneAsync(int delta, CancellationToken ct)
    {
        var sceneListResp = await _client.SendRequestAsync("GetSceneList", null, ct).ConfigureAwait(false);
        var scenes = sceneListResp.GetProperty("responseData").GetProperty("scenes").EnumerateArray()
            .Select(x => x.GetProperty("sceneName").GetString() ?? "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (scenes.Count == 0)
        {
            Log.Warn("[OBS] No scenes found.");
            return;
        }

        var curResp = await _client.SendRequestAsync("GetCurrentProgramScene", null, ct).ConfigureAwait(false);
        var cur = curResp.GetProperty("responseData").GetProperty("currentProgramSceneName").GetString() ?? "";

        var idx = scenes.FindIndex(x => string.Equals(x, cur, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = 0;

        var next = (idx + delta) % scenes.Count;
        if (next < 0) next += scenes.Count;

        await _client.SendRequestAsync("SetCurrentProgramScene", new { sceneName = scenes[next] }, ct).ConfigureAwait(false);
    }

    private async Task ToggleSpecialInputMuteAsync(string key, CancellationToken ct)
    {
        var specialResp = await _client.SendRequestAsync("GetSpecialInputs", null, ct).ConfigureAwait(false);
        var data = specialResp.GetProperty("responseData");

        // mic1 / desktop1
        if (!data.TryGetProperty(key, out var inputNameEl))
        {
            Log.Warn($"[OBS] Special input '{key}' not found.");
            return;
        }

        var inputName = inputNameEl.GetString();
        if (string.IsNullOrWhiteSpace(inputName))
        {
            Log.Warn($"[OBS] Special input '{key}' is empty.");
            return;
        }

        await _client.SendRequestAsync("ToggleInputMute", new { inputName }, ct).ConfigureAwait(false);
    }
}