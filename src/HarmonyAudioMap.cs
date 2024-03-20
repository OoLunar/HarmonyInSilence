using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeepgramSharp;
using DeepgramSharp.Entities;
using DSharpPlus.VoiceLink;

namespace OoLunar.HarmonyInSilence
{
    public sealed record HarmonyAudioMap
    {
        // PCM 16-bit, 48kHz, 2 channels, 20ms
        private static readonly byte[] SilenceFrame = new byte[9600];

        public DeepgramLivestreamApi Connection { get; init; }
        public Dictionary<VoiceLinkUser, int> UserChannels { get; init; }
        public int AvailableChannels => _maxChannelCount - UserChannels.Count;
        private readonly int _maxChannelCount;

        public HarmonyAudioMap(DeepgramLivestreamApi connection, VoiceLinkUser user, int maxChannelCount = 255)
        {
            Connection = connection;
            UserChannels = new() { { user, 0 } };
            _maxChannelCount = maxChannelCount;
            _ = StartSendingTranscriptionAsync();
            _ = StartReceivingTranscriptionAsync();
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

        public async Task StartSendingTranscriptionAsync()
        {
            // Send a frame of silence to prevent the connection from closing
            await Connection.SendAudioAsync(SilenceFrame);

            // Ensure we're never waiting more than 5 seconds for audio data,
            // Deepgram will close the connection after 10 seconds of no audio being sent.
            CancellationTokenSource timeoutCts = new();

            // This will be broken when the user disconnects from the VC.
            while (true)
            {
                VoiceLinkUser VoiceLinkUser = UserChannels.Keys.First();

                // Rent out 5 seconds to read audio
                ResetCancellationToken(VoiceLinkUser.AudioPipe, ref timeoutCts);

                // Attempt to get the user's voice data
                ReadResult result = await VoiceLinkUser.AudioPipe.ReadAsync();
                if (Connection.State != WebSocketState.Open)
                {
                    // Always mark the read as finished.
                    VoiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.End);
                    break;
                }
                else if (result.IsCanceled)
                {
                    // The result was cancelled from a timeout, send a frame of silence
                    VoiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.Start, result.Buffer.End);
                    await Connection.SendAudioAsync(SilenceFrame);
                    continue;
                }

                // Send the audio data for transcription
                await Connection.SendAudioAsync(result.Buffer.ToArray());

                // Always mark the read as finished.
                VoiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.End);

                // The user disconnected from the VC.
                if (result.IsCompleted)
                {
                    // Tell Deepgram that we're not going to be sending any more audio.
                    await Connection.RequestClosureAsync();
                    return;
                }
            }
        }

        public async Task StartReceivingTranscriptionAsync()
        {
            bool isOpen = true;
            StringBuilder stringBuilder = new();
            PeriodicTimer timeoutTimer = new(TimeSpan.FromSeconds(5));
            while (await timeoutTimer.WaitForNextTickAsync() && isOpen)
            {
                foreach (VoiceLinkUser VoiceLinkUser in UserChannels.Keys)
                {
                    stringBuilder.Clear();
                    DeepgramLivestreamResponse? response;
                    while ((response = Connection.ReceiveTranscription()) is not null)
                    {
                        if (response.Type is DeepgramLivestreamResponseType.Transcript)
                        {
                            string? transcription = response.Transcript?.Channel.Alternatives[0].Transcript;
                            if (response.Transcript!.IsFinal)
                            {
                                stringBuilder.Append(transcription);
                            }
                        }
                        else if (response.Type is DeepgramLivestreamResponseType.Closed)
                        {
                            isOpen = false;
                        }
                    }

                    if (stringBuilder.Length > 0)
                    {
                        await VoiceLinkUser.Connection.Channel.SendMessageAsync($"{VoiceLinkUser.Member.DisplayName}: {stringBuilder}");
                    }
                }
            }
        }

        private static void ResetCancellationToken(PipeReader pipeReader, ref CancellationTokenSource cancellationTokenSource)
        {
            if (!cancellationTokenSource.TryReset())
            {
                cancellationTokenSource = new(TimeSpan.FromSeconds(5));
                cancellationTokenSource.Token.Register(pipeReader.CancelPendingRead);
            }
        }
    }
}
