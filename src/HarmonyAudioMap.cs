using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepgramSharp;
using DSharpPlus.VoiceLink;
using DSharpPlus.VoiceLink.Opus;

namespace OoLunar.HarmonyInSilence
{
    public sealed record HarmonyAudioMap
    {
        private const int SAMPLE_RATE = 48000; // 48kHz
        private const int FRAME_DURATION_MS = 20; // 20ms
        private const int BYTES_PER_SAMPLE = 2; // assuming 16-bit audio
        private const int SAMPLES_PER_FRAME = SAMPLE_RATE * FRAME_DURATION_MS / 1000;
        private const int BYTES_PER_FRAME = SAMPLES_PER_FRAME * BYTES_PER_SAMPLE;

        public DeepgramLivestreamApi Connection { get; init; }
        public Dictionary<VoiceLinkUser, int> UserChannels { get; init; }
        public int AvailableChannels => _maxChannelCount - UserChannels.Count;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly int _maxChannelCount;

        public HarmonyAudioMap(DeepgramLivestreamApi connection, VoiceLinkUser user, int maxChannelCount = 255)
        {
            Connection = connection;
            UserChannels = new() { { user, 0 } };
            _maxChannelCount = maxChannelCount;
            _ = StartTranscriptionAsync();
        }

        public bool TryAddUser(VoiceLinkUser user)
        {
            int channelPosition = AvailableChannels;
            if (channelPosition == 0 || UserChannels.ContainsKey(user))
            {
                return false;
            }

            UserChannels.Add(user, channelPosition);
            return true;
        }

        public bool TryRemoveUser(VoiceLinkUser user) => UserChannels.Remove(user);

        private async Task StartTranscriptionAsync()
        {
            // Create a timeout token source to cancel any tasks waiting for unreceived audio
            CancellationTokenSource _timeoutTokenSource = new();

            // Periodically send audio to Deepgram
            PeriodicTimer timer = new(TimeSpan.FromMilliseconds(20));

            // 512kb buffer
            byte[] buffer = new byte[512 * 1024];
            while (await timer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                // Write audio data to the buffer
                int bytesWritten = WriteToBuffer(buffer, ref _timeoutTokenSource);

                // Send the audio to Deepgram
                await Connection.SendAudioAsync(buffer.AsMemory(0, bytesWritten), _cancellationTokenSource.Token);

                // Clear the buffer
                Array.Clear(buffer, 0, bytesWritten);

                // Needs Testing: In theory I will always be filling the entire buffer
                // which means I can clear all of it instead of just the written bytes
                // However because this is untested, I will leave it as is.
            }

            int WriteToBuffer(Span<byte> buffer, ref CancellationTokenSource _timeoutTokenSource)
            {
                // Set the timeout token to cancel after 20ms - the amount of time available in an audio frame.
                _timeoutTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

                // Copy audio data from each user to the buffer
                int bufferOffset = 0;

                //foreach (VoiceLinkUser user in UserChannels.Keys)
                //{
                //    // If data is available, copy it to the buffer
                //    // Otherwise it'll be zero filled which is silence
                //    if (TryGetChannelData(user, out ReadOnlySpan<byte> data))
                //    {
                //        // Copy data to the corresponding channel in the buffer
                //        data.CopyTo(buffer[bufferOffset..]);
                //    }
                //
                //    bufferOffset += data.Length;
                //}

                // Reset the timeout token ASAP so we can possibly reuse it
                if (!_timeoutTokenSource.TryReset())
                {
                    _timeoutTokenSource = new();
                }

                // Fill the rest of the channels with Opus silence packets
                if (AvailableChannels > 0)
                {
                    // Create a silence packet
                    OpusEncoder encoder = OpusEncoder.Create(OpusSampleRate.Opus48000Hz, 1, OpusApplication.Voip);
                    Span<byte> frame = buffer[bufferOffset..];
                    int bytesWritten = encoder.Encode([], 120, ref frame);

                    // Copy the silence packet to the buffer per however many channels are available
                    Span<byte> silence = frame[bufferOffset..bytesWritten];
                    int i = AvailableChannels;
                    while (i > 0)
                    {
                        silence.CopyTo(buffer[bufferOffset..]);
                        bufferOffset += silence.Length;
                        i--;
                    }
                }

                // Also known as the number of bytes written to the buffer
                return bufferOffset;
            }
        }

        private static bool TryGetChannelData(VoiceLinkUser user, out ReadOnlySpan<byte> data)
        {
            if (user.AudioPipe.TryRead(out ReadResult readResult))
            {
                // Check if there is enough data for one frame
                data = readResult.Buffer.ToArray();
                user.AudioPipe.AdvanceTo(readResult.Buffer.GetPosition(BYTES_PER_FRAME));
                return true;
            }

            user.AudioPipe.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
            data = default;
            return false;
        }
    }
}
