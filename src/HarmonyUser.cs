using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeepgramSharp;
using DeepgramSharp.Entities;
using DSharpPlus.VoiceLink;

namespace OoLunar.HarmonyInSilence
{
    public sealed record HarmonyUser
    {
        private static readonly byte[] SilenceFrame = [0xF8, 0xFF, 0xFE];

        public required VoiceLinkUser VoiceLinkUser { get; init; }
        public required DeepgramLivestreamApi SubtitleConnection { get; init; }
        public required int Channel { get; init; }

        public async Task StartSendingTranscriptionAsync()
        {
            if (!Directory.Exists("tests"))
            {
                Directory.CreateDirectory("tests");
            }

            if (File.Exists($"tests/{VoiceLinkUser.Member.Id}.wav"))
            {
                File.Delete($"tests/{VoiceLinkUser.Member.Id}.wav");
            }

            using FileStream fileStream = File.Create($"tests/{VoiceLinkUser.Member.Id}.pcm");

            // Send a frame of silence to prevent the connection from closing
            await SubtitleConnection.SendAudioAsync(SilenceFrame);

            // Ensure we're never waiting more than 5 seconds for audio data,
            // Deepgram will close the connection after 10 seconds of no audio being sent.
            CancellationTokenSource timeoutCts = new();

            // This will be broken when the user disconnects from the VC.
            while (true)
            {
                // Rent out 5 seconds to read audio
                ResetCancellationToken(VoiceLinkUser.AudioPipe, ref timeoutCts);

                // Attempt to get the user's voice data
                ReadResult result = await VoiceLinkUser.AudioPipe.ReadAsync();
                if (SubtitleConnection.State != WebSocketState.Open)
                {
                    // Always mark the read as finished.
                    VoiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.End);
                    break;
                }
                else if (result.IsCanceled)
                {
                    // The result was cancelled from a timeout, send a frame of silence
                    VoiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.Start, result.Buffer.End);
                    await SubtitleConnection.SendAudioAsync(SilenceFrame);
                    await fileStream.WriteAsync(SilenceFrame);
                    await fileStream.FlushAsync();
                    continue;
                }

                // Send the audio data for transcription
                await SubtitleConnection.SendAudioAsync(result.Buffer.ToArray());
                await fileStream.WriteAsync(result.Buffer.ToArray());
                await fileStream.FlushAsync();

                // The user disconnected from the VC.
                if (result.IsCompleted)
                {
                    await SubtitleConnection.RequestClosureAsync();
                }

                // Always mark the read as finished.
                VoiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.End);
            }
        }

        public async Task StartReceivingTranscriptionAsync()
        {
            PeriodicTimer timeoutTimer = new(TimeSpan.FromSeconds(5));
            while (await timeoutTimer.WaitForNextTickAsync())
            {
                StringBuilder stringBuilder = new();
                DeepgramLivestreamResponse? response;
                while ((response = SubtitleConnection.ReceiveTranscription()) is not null)
                {
                    if (response.Type is DeepgramLivestreamResponseType.Closed)
                    {
                        throw new InvalidOperationException($"Subtitle connection was closed: {response.Error?.ToString() ?? response.Metadata?.ToString() ?? "No error or metadata provided."}");
                    }
                    else if (response.Type is DeepgramLivestreamResponseType.Transcript)
                    {
                        string? transcription = response.Transcript?.Channel.Alternatives[0].Transcript;
                        if (response.Transcript!.IsFinal)
                        {
                            stringBuilder.Append(transcription);
                        }
                    }
                }

                await VoiceLinkUser.Connection.Channel.SendMessageAsync($"{VoiceLinkUser.Member.DisplayName}: {stringBuilder}");
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
