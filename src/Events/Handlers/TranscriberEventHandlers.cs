using System;
using System.Threading.Tasks;
using DSharpPlus.VoiceLink;
using DSharpPlus.VoiceLink.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OoLunar.HarmonyInSilence.Audio;

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
            if (!_userMapper.IsBeingTranscribed(eventArgs.VoiceUser) && !await _userMapper.TryAddTranscriberAsync(eventArgs.VoiceUser))
            {
                // TODO: Add a queue maybe?
                await eventArgs.Channel.SendMessageAsync($"{extension.Client.CurrentUser.Mention}: Heads up, I'm currently at my transcription limit. I can't transcribe any more users right now. I'm sorry {eventArgs.User.Mention}!");
            }
        }

        [DiscordEvent]
        public async Task UserDisconnectAsync(VoiceLinkExtension extension, VoiceLinkUserEventArgs eventArgs)
        {
            // The voice user will be null if the user never spoke.
            if (eventArgs.VoiceUser is null)
            {
                return;
            }

            // User left a channel
            _logger.LogDebug("User {UserId} left channel {ChannelId} of guild {GuildId}", eventArgs.Member.Id, eventArgs.Connection.Channel.Id, eventArgs.Connection.Guild.Id);
            await _userMapper.RemoveTranscriberAsync(eventArgs.VoiceUser);
            if (eventArgs.Connection.Channel.Users.Count == 1)
            {
                await eventArgs.Connection.Channel.SendMessageAsync($"{extension.Client.CurrentUser.Mention}: I'm all alone now. Thank you for hanging out with me! I'm gonna go though. Have a good time!");
                await eventArgs.Connection.DisconnectAsync();
            }
        }
    }
}
