using System;

namespace TabletDeck
{
    public static class MediaKeys
    {
        private const float VolumeStep = 0.04f; // 4%

        public static void Send(string cmd)
        {
            cmd = (cmd ?? "").Trim().ToLowerInvariant();
            if (cmd.Length == 0) return;

            Log.Info($"[MEDIA] cmd='{cmd}'");

            // 1) GŁOŚNOŚĆ / MUTE -> CoreAudio + fallback do klawiszy
            if (cmd == "volup")
            {
                try { CoreAudioVolume.Step(+VolumeStep); }
                catch (Exception ex)
                {
                    Log.Warn($"[VOL] Step(+): {ex.GetType().Name}: {ex.Message} -> fallback key");
                    WinSendInputMedia.TrySend("volup");
                }
                return;
            }

            if (cmd == "voldown")
            {
                try { CoreAudioVolume.Step(-VolumeStep); }
                catch (Exception ex)
                {
                    Log.Warn($"[VOL] Step(-): {ex.GetType().Name}: {ex.Message} -> fallback key");
                    WinSendInputMedia.TrySend("voldown");
                }
                return;
            }

            if (cmd == "mute")
            {
                try { CoreAudioVolume.ToggleMute(); }
                catch (Exception ex)
                {
                    Log.Warn($"[VOL] ToggleMute: {ex.GetType().Name}: {ex.Message} -> fallback key");
                    WinSendInputMedia.TrySend("mute");
                }
                return;
            }

            // 2) TRANSPORT -> NAJPEWNIEJ klawisze (YouTube/Netflix)
            if (WinSendInputMedia.TrySend(cmd))
                return;

            // 3) (opcjonalnie) GSMTC jako fallback
            try
            {
                if (GsmTcMediaTransport.TrySend(cmd))
                {
                    Log.Info("[MEDIA] GSMTC handled");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[MEDIA] GSMTC failed: {ex.GetType().Name}: {ex.Message}");
            }

            Log.Warn($"[MEDIA] not handled cmd='{cmd}'");
        }
    }
}
