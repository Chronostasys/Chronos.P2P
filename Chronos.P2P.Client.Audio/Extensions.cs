using Chronos.P2P.Server;
using NAudio.Wave;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Chronos.P2P.Client.Audio
{
    public class AudioLiveStreamHandler
    {
        static BufferedWaveProvider provider = new BufferedWaveProvider(new WaveFormat());
        static WaveOutEvent wo = null;

        public AudioLiveStreamHandler(Peer peer)
        {
            if (wo is null)
            {
                wo = new WaveOutEvent();
            }
        }
        [Handler((int)CallMethods.AudioDataSlice)]
        public void OnAudioDataSliceGet(UdpContext context)
        {
            var slice = context.GetData<DataSlice>().Data;
            Console.WriteLine(slice.No);
            provider.DiscardOnBufferOverflow = true;
            provider.AddSamples(slice.Slice, 0, slice.Slice.Length);
            if (wo.PlaybackState is not PlaybackState.Playing)
            {
                try
                {
                    wo.DesiredLatency = 400;
                    wo.Init(provider);
                    wo.Play();
                }
                catch (Exception)
                {

                    throw;
                }
            }
            
        }
    }
    public static class Extensions
    {
        public static void StartSendLiveAudio(this Peer peer, string name)
        {
            peer.AddHandler<AudioLiveStreamHandler>();
            var channel = Channel.CreateUnbounded<(byte[], int)>();
            _ = peer.SendLiveStreamAsync(channel, name, (int)CallMethods.AudioDataSlice);
            var capture = new WaveInEvent();
            capture.WaveFormat = new WaveFormat();
            capture.BufferMilliseconds = 100;
            capture.DataAvailable += async (object sender, WaveInEventArgs e) =>
            {
                await channel.Writer.WaitToWriteAsync();
                await channel.Writer.WriteAsync((e.Buffer, e.BytesRecorded));
            };
            capture.StartRecording();
        }

    }
}
