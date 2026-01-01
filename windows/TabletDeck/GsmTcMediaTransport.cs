// GsmTcMediaTransport.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace TabletDeck
{
    internal static class GsmTcMediaTransport
    {
        private static GlobalSystemMediaTransportControlsSessionManager? _mgr;
        private static DateTime _mgrAt;

        public static bool TrySend(string cmd)
            => TrySendAsync(cmd).GetAwaiter().GetResult();

        private static async Task<bool> TrySendAsync(string cmd)
        {
            cmd = (cmd ?? "").Trim().ToLowerInvariant();
            if (cmd.Length == 0) return false;

            try
            {
                var mgr = await GetManagerAsync();
                if (mgr == null) return false;

                // 1) Prefer current session
                var session = mgr.GetCurrentSession();

                // 2) Fallback: choose any session with playable controls
                if (session == null)
                {
                    var sessions = mgr.GetSessions();
                    session = sessions.FirstOrDefault(s =>
                    {
                        var c = s?.GetPlaybackInfo()?.Controls;
                        return c != null && (c.IsPlayEnabled || c.IsPauseEnabled || c.IsNextEnabled || c.IsPreviousEnabled);
                    });
                }

                if (session == null)
                {
                    Log.Warn("[MEDIA] GSMTC: no session");
                    return false;
                }

                var controls = session.GetPlaybackInfo()?.Controls;
                if (controls == null)
                {
                    Log.Warn("[MEDIA] GSMTC: no controls");
                    return false;
                }

                switch (cmd)
                {
                    case "playpause":
                        if (controls.IsPlayEnabled || controls.IsPauseEnabled)
                            return (await session.TryTogglePlayPauseAsync());
                        return false;

                    case "next":
                        if (controls.IsNextEnabled)
                            return (await session.TrySkipNextAsync());
                        return false;

                    case "prev":
                        if (controls.IsPreviousEnabled)
                            return (await session.TrySkipPreviousAsync());
                        return false;

                    case "stop":
                        // “Stop” często nie jest wspierany w GSMTC; robimy bezpieczny fallback: Pause
                        if (controls.IsPauseEnabled)
                            return (await session.TryPauseAsync());
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[MEDIA] GSMTC exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static async Task<GlobalSystemMediaTransportControlsSessionManager?> GetManagerAsync()
        {
            // Odświeżaj manager co jakiś czas (czasem sesje się “rozjeżdżają”)
            if (_mgr != null && (DateTime.UtcNow - _mgrAt) < TimeSpan.FromSeconds(15))
                return _mgr;

            _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _mgrAt = DateTime.UtcNow;
            return _mgr;
        }
    }
}
