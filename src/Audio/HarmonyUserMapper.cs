using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeepgramSharp;
using DSharpPlus.VoiceLink;
using Microsoft.Extensions.Logging;
using OoLunar.HarmonyInSilence.Configuration;

namespace OoLunar.HarmonyInSilence.Audio
{
    public sealed class HarmonyUserMapper
    {
        private readonly DeepgramClient _deepgramClient;
        private readonly List<HarmonyAudioMap> _activeConnections = [];
        private readonly Dictionary<ulong, HarmonyAudioMap> _transcriberMap = [];
        private readonly ILogger<HarmonyUserMapper> _logger;
        private readonly ILogger<HarmonyAudioMap> _audioLogger;
        private readonly int _maxChannelCount;

        public HarmonyUserMapper(DeepgramClient deepgramClient, ILogger<HarmonyUserMapper> logger, ILogger<HarmonyAudioMap> audioLogger, HarmonyConfiguration configuration)
        {
            _deepgramClient = deepgramClient ?? throw new ArgumentNullException(nameof(deepgramClient));
            _maxChannelCount = configuration.Deepgram.MaxChannelCount;
            _logger = logger;
            _audioLogger = audioLogger;
        }

        public bool IsBeingTranscribed(VoiceLinkUser user) => _transcriberMap.ContainsKey(user.Member.Id);

        public async ValueTask<bool> TryAddTranscriberAsync(VoiceLinkUser user)
        {
            if (_transcriberMap.ContainsKey(user.Member.Id))
            {
                _logger.LogDebug("User {UserId} is already being transcribed", user.Member.Id);
                return false;
            }

            HarmonyAudioMap? map = _activeConnections.FirstOrDefault(map => map.TryAddUser(user));
            if (map is not null)
            {
                _logger.LogInformation("Added user {UserId} to existing transcription map", user.Member.Id);
                return true;
            }

            _logger.LogInformation("Creating new connection that can handle {ChannelCount} channels", _maxChannelCount);
            map = new HarmonyAudioMap(user, _maxChannelCount, _audioLogger);
            _activeConnections.Add(map);
            _transcriberMap.Add(user.Member.Id, map);

            _logger.LogInformation("Added user {UserId} to new transcription map", user.Member.Id);
            await map.StartAsync(_deepgramClient);
            return true;
        }

        public async ValueTask RemoveTranscriberAsync(VoiceLinkUser user)
        {
            if (!_transcriberMap.TryGetValue(user.Member.Id, out HarmonyAudioMap? map))
            {
                _logger.LogWarning("Failed to remove user {UserId} from the transcriber map as they are not being transcribed!", user);
                return;
            }

            _logger.LogInformation("Removing user {UserId} from transcription map", user);
            map.TryRemoveUser(user);
            if (map.IsEmpty && _activeConnections.Remove(map))
            {
                _logger.LogInformation("Transcription map is empty, stopping transcription");
                _transcriberMap.Remove(user.Member.Id);
                await map.DisposeAsync();
            }
        }
    }
}
