using NAudio.CoreAudioApi;
using System;
using System.Data;

namespace TabletDeck
{
    public static class CoreAudioVolume
    {
        public static void Step(float delta)
        {
            using var dev = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            float v = dev.AudioEndpointVolume.MasterVolumeLevelScalar;
            v = Math.Clamp(v + delta, 0f, 1f);
            dev.AudioEndpointVolume.MasterVolumeLevelScalar = v;

            Log.Info($"[VOL] set={v:0.00}");
        }

        public static void ToggleMute()
        {
            using var dev = new MMDeviceEnumerator()
                .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            dev.AudioEndpointVolume.Mute = !dev.AudioEndpointVolume.Mute;
            Log.Info($"[VOL] mute={dev.AudioEndpointVolume.Mute}");
        }
    }
}
