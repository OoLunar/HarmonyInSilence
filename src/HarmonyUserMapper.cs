using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DeepgramSharp;
using DeepgramSharp.Entities;
using DSharpPlus.VoiceLink;
using Microsoft.Extensions.Logging;
using OoLunar.HarmonyInSilence.Configuration;

namespace OoLunar.HarmonyInSilence
{
    public sealed class HarmonyUserMapper
    {
        private readonly DeepgramClient _deepgramClient;
        //private readonly List<HarmonyAudioMap> _activeConnections = [];
        //private readonly Dictionary<VoiceLinkUser, HarmonyAudioMap> _transcriberMap = [];
        //private readonly ILogger<HarmonyUserMapper> _logger;
        private readonly int _maxChannelCount;
        private readonly Dictionary<VoiceLinkUser, HarmonyUser> _userMap = [];

        public HarmonyUserMapper(DeepgramClient deepgramClient, ILogger<HarmonyUserMapper> logger, HarmonyConfiguration configuration)
        {
            _deepgramClient = deepgramClient ?? throw new ArgumentNullException(nameof(deepgramClient));
            _maxChannelCount = configuration.Deepgram.MaxChannelCount;
            //_logger = logger ?? NullLogger<HarmonyUserMapper>.Instance;
        }

        public async ValueTask AddTranscriberAsync(VoiceLinkUser user)
        {
            //if (_transcriberMap.ContainsKey(user))
            //{
            //    _logger.LogDebug("User {UserId} is already being transcribed", user.Member.Id);
            //    return;
            //}
            //
            //HarmonyAudioMap? map = _activeConnections.FirstOrDefault(map => map.TryAddUser(user));
            //if (map is not null)
            //{
            //    _logger.LogInformation("Added user {UserId} to existing transcription map", user.Member.Id);
            //    return;
            //}
            //
            //_logger.LogInformation("Creating new connection that can handle {ChannelCount} channels", _maxChannelCount);
            //map = new HarmonyAudioMap(livestreamApi, user, _maxChannelCount);
            //_activeConnections.Add(map);
            //_transcriberMap.Add(user, map);
            //_logger.LogInformation("Added user {UserId} to new transcription map", user.Member.Id);

            DeepgramLivestreamApi livestreamApi = await _deepgramClient.CreateLivestreamAsync(new()
            {
                Channels = 2,
                SampleRate = 48000,
                //MultiChannel = true,
                Encoding = DeepgramEncoding.Linear16,
                Model = "nova-2",
                InterimResults = true
            });

            HarmonyUser harmonyUser = new()
            {
                VoiceLinkUser = user,
                SubtitleConnection = livestreamApi,
                Channel = 0
            };

            _ = harmonyUser.StartSendingTranscriptionAsync();
            _ = harmonyUser.StartReceivingTranscriptionAsync();
            _userMap.Add(user, harmonyUser);
        }

        public ValueTask<bool> RemoveTranscriberAsync(ulong userId)
        {
            foreach (VoiceLinkUser user in _userMap.Keys)
            {
                if (user.Member.Id == userId)
                {
                    _userMap.Remove(user);
                    return ValueTask.FromResult(true);
                }
            }

            return ValueTask.FromResult(false);
        }
    }
}
