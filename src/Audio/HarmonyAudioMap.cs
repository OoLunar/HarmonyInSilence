using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DeepgramSharp;
using DeepgramSharp.Entities;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink;
using Microsoft.Extensions.Logging;

namespace OoLunar.HarmonyInSilence.Audio
{
    public sealed record HarmonyAudioMap : IAsyncDisposable
    {
        // 16 bits per sample
        private const int BYTES_PER_SAMPLE = 2;

        // 48 kHz
        private const int SAMPLE_RATE = 48000;

        // 20 milliseconds
        private const double FRAME_DURATION = 0.020;

        // 960 samples
        private const int FRAME_SIZE = (int)(SAMPLE_RATE * FRAME_DURATION);

        // 20 milliseconds of audio data, 1920 bytes
        private const int SINGLE_CHANNEL_BUFFER_SIZE = FRAME_SIZE * BYTES_PER_SAMPLE;

        public int AvailableChannels => _maxChannelCount - _userChannels.Count;
        public bool IsEmpty => _userChannels.Count == 0;

        private Task? _receivingTask;
        private Task? _sendingTask;
        private DeepgramLivestreamApi? _connection;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Dictionary<int, VoiceLinkUser> _userChannels;
        private readonly ILogger<HarmonyAudioMap> _logger;
        private readonly int _maxChannelCount;
        private readonly object _addLock = new();

        public HarmonyAudioMap(VoiceLinkUser user, int maxChannelCount, ILogger<HarmonyAudioMap> logger)
        {
            _userChannels = new() { { 0, user } };
            _maxChannelCount = maxChannelCount;
            _logger = logger;
        }

        public async ValueTask StartAsync(DeepgramClient deepgramClient)
        {
            _connection = await deepgramClient.CreateLivestreamAsync(new()
            {
                Channels = _maxChannelCount,
                SampleRate = 48000,
                MultiChannel = true,
                Encoding = DeepgramEncoding.Linear16,
                Model = "nova-2",
                InterimResults = true,
                Punctuate = true,
                SmartFormat = true,
                FillerWords = true
            });

            _receivingTask = StartReceivingTranscriptionAsync();
            _sendingTask = StartSendingTranscriptionAsync(_cancellationTokenSource.Token);
        }

        public bool TryAddUser(VoiceLinkUser user)
        {
            lock (_addLock)
            {
                if (AvailableChannels == 0 || _userChannels.ContainsValue(user))
                {
                    return false;
                }

                _userChannels.Add(AvailableChannels, user);
                return true;
            }
        }

        public bool TryRemoveUser(VoiceLinkUser user)
        {
            lock (_addLock)
            {
                foreach (KeyValuePair<int, VoiceLinkUser> pair in _userChannels)
                {
                    if (pair.Value == user)
                    {
                        return _userChannels.Remove(pair.Key);
                    }
                }
            }

            return false;
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellationTokenSource.CancelAsync();
            if (_sendingTask is not null)
            {
                await _sendingTask;
            }

            if (_connection is not null)
            {
                await _connection.RequestClosureAsync();
            }

            if (_receivingTask is not null)
            {
                await _receivingTask;
            }

            _sendingTask = null;
            _receivingTask = null;
            _connection = null;
            _cancellationTokenSource.Dispose();
        }

        public async Task StartReceivingTranscriptionAsync()
        {
            // Create the message builder here and reuse it for every transcription
            DiscordMessageBuilder messageBuilder = new();
            messageBuilder.AddMentions(Mentions.None);

            PeriodicTimer timeoutTimer = new(TimeSpan.FromSeconds(5));
            while (await timeoutTimer.WaitForNextTickAsync() && _connection is not null)
            {
                DeepgramLivestreamResponse? response = _connection.ReceiveTranscription();
                if (response is null)
                {
                    continue;
                }
                else if (response.Type is DeepgramLivestreamResponseType.Closed)
                {
                    break;
                }
                else if (response.Type is DeepgramLivestreamResponseType.Error)
                {
                    _logger.LogError(response.Error, "Received error from Deepgram API, assuming corrupted state and closing connection.");
                    break;
                }
                else if (response.Type is DeepgramLivestreamResponseType.Transcript && response.Transcript is not null)
                {
                    _logger.LogInformation("Received {TranscriptionCount} transcriptions", response.Transcript.Channel.Alternatives.Count);
                    for (int i = 0; i < response.Transcript.Channel.Alternatives.Count; i++)
                    {
                        string? transcription = response.Transcript.Channel.Alternatives[i].Transcript;
#if !DEBUG
                        // Only send final transcriptions to the channel when the bot is in production
                        if (!response.Transcript!.IsFinal || transcription.Length == 0)
                        {
                            continue;
                        }
#endif
                        // If the user leaves while we're receiving their last transcription, we won't be able to locate the user in the dictionary.
                        if (_userChannels.TryGetValue(i, out VoiceLinkUser? voiceLinkUser))
                        {
                            messageBuilder.Content = $"{voiceLinkUser.Member.Mention}: {transcription}";
                            await voiceLinkUser.Connection.Channel.SendMessageAsync(messageBuilder);
                        }
                    }
                }
            }
        }

        public async Task StartSendingTranscriptionAsync(CancellationToken cancellationToken = default)
        {
            // Ensure we're not going to send data to a broken connection.
            if (cancellationToken.IsCancellationRequested || _connection is null)
            {
                return;
            }

            // The buffer size is the size of a single channel's audio data
            int bufferSize = SINGLE_CHANNEL_BUFFER_SIZE * _maxChannelCount;

            // This is the byte array that'll actually be sent to the connection
            byte[] pcmData = ArrayPool<byte>.Shared.Rent(bufferSize);
            pcmData.AsSpan().Clear();

            // Allocate a buffer for the PCM data, just to store everyone's audio data
            byte[] pcmBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            pcmBuffer.AsSpan().Clear();

            // Send a frame of silence to prevent the connection from closing
            await _connection.SendAudioAsync(pcmData, CancellationToken.None);

            // Ensure we're never waiting more than 5 seconds for audio data,
            // Deepgram will close the connection after 10 seconds of no audio being sent.
            CancellationTokenSource timeoutCts = new();

            // This will be broken when the user disconnects from the VC.
            while (!cancellationToken.IsCancellationRequested)
            {
                for (int channel = 0; channel < _maxChannelCount; channel++)
                {
                    int channelOffset = channel * SINGLE_CHANNEL_BUFFER_SIZE;
                    if (!_userChannels.TryGetValue(channel, out VoiceLinkUser? voiceLinkUser))
                    {
                        // Fill this audio channel with silence
                        Array.Fill<byte>(pcmBuffer, 0, channelOffset, SINGLE_CHANNEL_BUFFER_SIZE);
                        continue;
                    }

                    // Spend 10ms to read 200ms of audio data
                    ResetCancellationToken(voiceLinkUser.AudioPipe, ref timeoutCts);

                    // Attempt to get the user's voice data
                    ReadResult result = await voiceLinkUser.AudioPipe.ReadAsync(CancellationToken.None);
                    if (result.IsCanceled)
                    {
                        // The result was cancelled from a timeout, send a frame of silence
                        _logger.LogDebug("Read was cancelled from a timeout, sending silence for user {UserId}", voiceLinkUser.Member.Id);
                        Array.Fill<byte>(pcmBuffer, 0, channelOffset, SINGLE_CHANNEL_BUFFER_SIZE);
                        voiceLinkUser.AudioPipe.AdvanceTo(result.Buffer.Start, result.Buffer.End);
                        continue;
                    }
                    else if (result.IsCompleted)
                    {
                        // User has disconnected, ensure they're removed from the dictionary to make sure we don't try reading from a finished pipe
                        _logger.LogDebug("User {UserId} has disconnected, removing from audio map", voiceLinkUser.Member.Id);
                        _userChannels.Remove(channel);
                        continue;
                    }

                    _logger.LogDebug("Read {BufferLength} bytes from user {UserId}", result.Buffer.Length, voiceLinkUser.Member.Id);
                    ReadOnlySequence<byte> buffer = result.Buffer.Slice(0, SINGLE_CHANNEL_BUFFER_SIZE);

                    // Write the user's audio data to the repacketizer
                    buffer.CopyTo(pcmBuffer.AsSpan(channelOffset));

                    // Always mark the read as finished.
                    voiceLinkUser.AudioPipe.AdvanceTo(buffer.End, buffer.End);
                }

                // Each user's audio data is in 1920 byte chunks, we need to merge them into their own PCM channels
                for (int channel = 0; channel < _maxChannelCount; channel++)
                {
                    int channelOffset = channel * SINGLE_CHANNEL_BUFFER_SIZE;
                    ReadOnlyMemory<byte> channelData = pcmBuffer.AsMemory(channelOffset, SINGLE_CHANNEL_BUFFER_SIZE);
                    for (int sample = 0; sample < SINGLE_CHANNEL_BUFFER_SIZE; sample += 2)
                    {
                        pcmData[channelOffset + sample] = channelData.Span[sample];
                        pcmData[channelOffset + sample + 1] = channelData.Span[sample + 1];
                    }
                }

                // Ensure we're not going to send data to a broken connection,
                // causing exceptions to be thrown and our buffers to not be returned.
                if (_connection.State != WebSocketState.Open)
                {
                    _logger.LogDebug("Connection is not open, breaking out of audio loop.");
                    break;
                }

                // Send the PCM data to the connection
                await _connection.SendAudioAsync(pcmData.AsMemory(0, bufferSize), CancellationToken.None);
            }

            ArrayPool<byte>.Shared.Return(pcmBuffer);
            ArrayPool<byte>.Shared.Return(pcmData);
        }

        private void ResetCancellationToken(PipeReader pipeReader, ref CancellationTokenSource cancellationTokenSource)
        {
            if (!cancellationTokenSource.TryReset())
            {
                cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
                cancellationTokenSource.Token.Register(pipeReader.CancelPendingRead);
            }
        }
    }
}


