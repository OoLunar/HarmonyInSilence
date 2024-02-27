using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceLink;
using DSharpPlus.VoiceLink.Enums;

namespace OoLunar.HarmonyInSilence.Commands
{
    public sealed class JoinCommand
    {
        [Command("join"), Description("Join a voice channel to start providing subtitles.")]
        [RequireGuild]
        public static async ValueTask ExecuteAsync(CommandContext context, DiscordChannel? channel = null)
        {
            if (channel is null)
            {
                if (context.Member?.VoiceState?.Channel is null)
                {
                    await context.RespondAsync("Please specify a voice channel to join.");
                    return;
                }

                channel = context.Member.VoiceState.Channel;
            }

            Permissions channelPermissions = channel.PermissionsFor(context.Guild!.CurrentMember);
            if (!channelPermissions.HasPermission(Permissions.AccessChannels))
            {
                await context.RespondAsync($"I don't have permission to see {channel.Mention}. Could you give me the {Formatter.InlineCode(Permissions.AccessChannels.ToPermissionString())} permission please?");
                return;
            }
            else if (!channelPermissions.HasPermission(Permissions.UseVoice))
            {
                await context.RespondAsync($"I don't have permission to connect to {channel.Mention}. Could you give me the {Formatter.InlineCode(Permissions.UseVoice.ToPermissionString())} permission please?");
                return;
            }
            else if (!channelPermissions.HasPermission(Permissions.SendMessages))
            {
                await context.RespondAsync($"I don't have permission to send messages in {channel.Mention}.");
                return;
            }
            else if (channel.Type is not ChannelType.Voice and not ChannelType.Stage)
            {
                await context.RespondAsync($"Channel {channel.Mention} is not a voice channel.");
                return;
            }

            VoiceLinkExtension voice = context.Client.GetVoiceLinkExtension();
            if (voice.Connections.TryGetValue(context.Guild.Id, out VoiceLinkConnection? connection))
            {
                await context.RespondAsync($"I'm already connected to {connection.Channel.Mention}.");
                return;
            }

            connection = await voice.ConnectAsync(channel, VoiceState.UserMuted);
            await context.RespondAsync($"Joined {channel.Mention}.");
        }
    }
}
