using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DeepgramSharp;
using DeepgramSharp.Entities;
using DSharpPlus.VoiceLink;

namespace OoLunar.HarmonyInSilence
{
    public sealed record HarmonyAudioMap
    {
        // Opus silence frame, 20ms, 48kHz, stereo
        private static readonly byte[] SilenceFrame = [0xf8, 0xff, 0xfe];

        public DeepgramLivestreamApi Connection { get; init; }
        public Dictionary<int, VoiceLinkUser> UserChannels { get; init; }
        public int AvailableChannels => _maxChannelCount - UserChannels.Count;
        private readonly int _maxChannelCount;
        private readonly object _addLock = new();

        public HarmonyAudioMap(DeepgramLivestreamApi connection, VoiceLinkUser user, int maxChannelCount = 255)
        {
            Connection = connection;
            UserChannels = new() { { 0, user } };
            _maxChannelCount = maxChannelCount;
            _ = StartSendingTranscriptionAsync();
            _ = StartReceivingTranscriptionAsync();
        }

        public bool TryAddUser(VoiceLinkUser user)
        {
            lock (_addLock)
            {
                if (AvailableChannels == 0 || UserChannels.ContainsValue(user))
                {
                    return false;
                }

                UserChannels.Add(AvailableChannels, user);
                return true;
            }
        }

        public bool TryRemoveUser(VoiceLinkUser user)
        {
            lock (_addLock)
            {
                foreach (KeyValuePair<int, VoiceLinkUser> pair in UserChannels)
                {
                    if (pair.Value == user)
                    {
                        return UserChannels.Remove(pair.Key);
                    }
                }
            }

            return false;
        }

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
                VoiceLinkUser VoiceLinkUser = UserChannels[0];

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
            PeriodicTimer timeoutTimer = new(TimeSpan.FromSeconds(5));
            while (await timeoutTimer.WaitForNextTickAsync() && isOpen)
            {
                DeepgramLivestreamResponse? response;
                while ((response = Connection.ReceiveTranscription()) is not null)
                {
                    if (response.Type is DeepgramLivestreamResponseType.Transcript && response.Transcript is not null)
                    {
                        for (int i = 0; i < response.Transcript.Channel.Alternatives.Count; i++)
                        {
                            string? transcription = response.Transcript.Channel.Alternatives[i].Transcript;
                            if (response.Transcript!.IsFinal && transcription.Length > 0)
                            {
                                VoiceLinkUser voiceLinkUser = UserChannels[i];
                                await voiceLinkUser.Connection.Channel.SendMessageAsync($"{voiceLinkUser.Member.DisplayName}: {transcription}");
                            }
                        }
                    }
                    else if (response.Type is DeepgramLivestreamResponseType.Closed)
                    {
                        isOpen = false;
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
