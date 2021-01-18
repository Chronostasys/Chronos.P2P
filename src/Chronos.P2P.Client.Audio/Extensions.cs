using Chronos.P2P.Server;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chronos.P2P.Client.Audio
{
    public static class Extensions
    {
        public static Task StartSendLiveAudio(this Peer peer, string name)
        {
            peer.AddHandler<AudioLiveStreamHandler>();
            var channel = new MsgQueue<(byte[], int)>();
            var t = peer.SendLiveStreamAsync(channel, name, (int)CallMethods.AudioDataSlice);
            var capture = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1),
                BufferMilliseconds = 100
            };
            capture.DataAvailable += (object sender, WaveInEventArgs e) =>
            {
                channel.Enqueue((e.Buffer, e.BytesRecorded));
            };
            capture.StartRecording();
            return t.AsTask();
        }
    }

    public class AudioLiveStreamHandler
    {
        private static readonly object key = new();
        private static readonly BufferedWaveProvider provider = new(new WaveFormat(16000, 1));
        private static Dictionary<long, DataSlice> audioSlices = new();
        private static volatile int current = 0;
        private static volatile bool first = true;
        private static int i = 0;
        private static DirectSoundOut wo = null;
        private ILogger<AudioLiveStreamHandler> _logger;

        public AudioLiveStreamHandler(Peer peer, ILogger<AudioLiveStreamHandler> logger)
        {
            _logger = logger;
            if (wo is null)
            {
                wo = new DirectSoundOut(50);
            }
        }

        [Handler((int)CallMethods.AudioDataSlice)]
        public void OnAudioDataSliceGet(UdpContext context)
        {
            var slice = DataSlice.FromBytes(context.Data, context);
            provider.DiscardOnBufferOverflow = true;

            lock (key)
            {
                if (slice.No == current)
                {
                    provider.AddSamples(slice.Slice.ToArray(), 0, slice.Slice.Length);
                    slice.Context.Dispose();
                    current++;
                    while (audioSlices.Remove(current, out slice))
                    {
                        provider.AddSamples(slice.Slice.ToArray(), 0, slice.Slice.Length);
                        slice.Context.Dispose();
                        current++;
                    }
                }
                else
                {
                    audioSlices[slice.No] = slice;
                }
                if (provider.BufferedDuration.TotalMilliseconds > 320)
                {
                    if (i > 10)
                    {
                        _logger.LogWarning($"high latency detected({provider.BufferedDuration.TotalMilliseconds}ms), try to catch on the live audio stream...");
                        provider.ClearBuffer();
                        i = 0;
                    }
                    else i++;
                }
                else i = 0;
                if (wo.PlaybackState is not PlaybackState.Playing)
                {
                    if (!first)
                    {
                        current++;
                    }
                    wo.Init(provider);
                    wo.Play();
                    first = false;
                }
            }
        }
    }
}