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
        public static async Task StartSendLiveAudio(this Peer peer, string name)
        {
            peer.AddHandler<AudioLiveStreamHandler>();
            var channel = new DatasliceSender();
            await peer.RequestSendLiveStreamAsync(channel, name, (int)CallMethods.AudioDataSlice);
            var capture = new WaveInEvent();
            capture.WaveFormat = new WaveFormat();
            capture.BufferMilliseconds = 100;
            capture.DataAvailable +=  (object sender, WaveInEventArgs e) =>
            {
                channel.Send(e.Buffer, e.BytesRecorded);
            };
            capture.StartRecording();
        }

    }
}
