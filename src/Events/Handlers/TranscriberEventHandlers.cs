using System;
using System.Threading.Tasks;
using DSharpPlus.VoiceLink;
using DSharpPlus.VoiceLink.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OoLunar.HarmonyInSilence.Events.Handlers
{
    public sealed class TranscriberEventHandlers
    {
        private readonly HarmonyUserMapper _userMapper;
        private readonly ILogger<TranscriberEventHandlers> _logger;

        public TranscriberEventHandlers(HarmonyUserMapper userMapper, ILogger<TranscriberEventHandlers> logger)
        {
            _userMapper = userMapper ?? throw new ArgumentNullException(nameof(userMapper));
            _logger = logger ?? NullLogger<TranscriberEventHandlers>.Instance;
        }

        [DiscordEvent]
        public async Task UserSpokeAsync(VoiceLinkExtension extension, VoiceLinkUserSpeakingEventArgs eventArgs)
        {
            _logger.LogDebug("User {UserId} spoke in channel {ChannelId} of guild {GuildId}", eventArgs.User.Id, eventArgs.Channel.Id, eventArgs.Guild.Id);
            await _userMapper.AddTranscriberAsync(eventArgs.VoiceUser);
        }
    }
}
