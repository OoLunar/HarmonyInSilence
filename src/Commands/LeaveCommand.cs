using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.VoiceLink;

namespace OoLunar.HarmonyInSilence.Commands
{
    public sealed class LeaveCommand
    {
        [Command("leave"), Description("Leaves the voice channel.")]
        [RequireGuild]
        public static async ValueTask ExecuteAsync(CommandContext context)
        {
            if (!context.Client.GetVoiceLinkExtension().Connections.TryGetValue(context.Guild!.Id, out VoiceLinkConnection? connection))
            {
                await context.RespondAsync("I am not connected to a voice channel.");
                return;
            }

            Permissions channelPermissions = connection.Channel.PermissionsFor(context.Member!);
            if (!channelPermissions.HasPermission(Permissions.MoveMembers) || !channelPermissions.HasPermission(Permissions.DeafenMembers))
            {
                await context.RespondAsync("You don't have permission to move me out of the voice channel.");
                return;
            }

            await connection.DisconnectAsync();
            await context.RespondAsync("Disconnected from the voice channel.");
        }
    }
}
