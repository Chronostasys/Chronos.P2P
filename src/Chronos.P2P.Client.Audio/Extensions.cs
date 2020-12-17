using Chronos.P2P.Server;
using NAudio.Wave;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Chronos.P2P.Client.Audio
{
    public class AudioLiveStreamHandler
    {
        static BufferedWaveProvider provider = new BufferedWaveProvider(new WaveFormat());
        static DirectSoundOut wo = null;
        static volatile int i = 0;
        static object key = new();

        public AudioLiveStreamHandler(Peer peer)
        {
            if (wo is null)
            {
                wo = new DirectSoundOut(50);
            }
        }
        [Handler((int)CallMethods.AudioDataSlice)]
        public void OnAudioDataSliceGet(UdpContext context)
        {
            var slice = context.GetData<DataSlice>().Data;
            provider.DiscardOnBufferOverflow = true;
            lock (key)
            {
                if (provider.BufferedDuration.TotalMilliseconds > 100 && i < 7)
                {
                    Console.WriteLine("high latency detected, try to catch on the live audio stream...");
                    provider.ClearBuffer();
                    i++;
                }
                provider.AddSamples(slice.Slice, 0, slice.Slice.Length);
                if (wo.PlaybackState is not PlaybackState.Playing)
                {
                    wo.Init(provider);
                    wo.Play();
                }
            }
            
        }
    }
    public static class Extensions
    {
        public static Task StartSendLiveAudio(this Peer peer, string name)
        {
            peer.AddHandler<AudioLiveStreamHandler>();
            var channel = Channel.CreateUnbounded<(byte[], int)>();
            var t = peer.SendLiveStreamAsync(channel, name, (int)CallMethods.AudioDataSlice);
            var capture = new WaveInEvent();
            capture.WaveFormat = new WaveFormat();
            capture.BufferMilliseconds = 100;
            capture.DataAvailable += async (object sender, WaveInEventArgs e) =>
            {
                await channel.Writer.WaitToWriteAsync();
                await channel.Writer.WriteAsync((e.Buffer, e.BytesRecorded));
            };
            capture.StartRecording();
            return t;
        }

    }
}
