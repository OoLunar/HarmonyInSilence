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
            if (!await _userMapper.TryAddTranscriberAsync(eventArgs.VoiceUser))
            {
                // TODO: Add a queue maybe?
                await eventArgs.Channel.SendMessageAsync($"Heads up, I'm currently at my transcription limit. I can't transcribe any more users right now. I'm sorry {eventArgs.User.Mention}!");
            }
        }

        //[DiscordEvent]
        //public async Task UserDisconnectAsync(VoiceLinkExtension extension, VoiceLinkUserEventArgs eventArgs)
        //{
        //    // User joined a channel
        //    if (eventArgs.Connection.Channel.Users.Contains(eventArgs.Member))
        //    {
        //        return;
        //    }
        //
        //    // User left a channel
        //    _logger.LogDebug("User {UserId} left channel {ChannelId} of guild {GuildId}", eventArgs.Member.Id, eventArgs.Connection.Channel.Id, eventArgs.Connection.Guild.Id);
        //    await _userMapper.RemoveTranscriberAsync(eventArgs.Member.Id);
        //}
    }
}
