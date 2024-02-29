using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Attributes;

namespace OoLunar.HarmonyInSilence.Commands
{
    public sealed class InviteCommand
    {
        [Command("invite"), Description("Sends the link to add the bot to a guild without an embed.")]
        public static ValueTask InviteAsync(CommandContext context)
        {
            Permissions requiredPermissions = GetSubcommandsPermissions(context.Extension.Commands.Values);
            requiredPermissions |= Permissions.AccessChannels; // View voice channels
            requiredPermissions |= Permissions.UseVoice; // Connect to voice channels
            requiredPermissions |= Permissions.SendMessages; // Send messages (subtitles) in voice channels

            StringBuilder stringBuilder = new();
            stringBuilder.Append("<https://discord.com/api/oauth2/authorize?client_id=");
            stringBuilder.Append(context.Client.CurrentUser.Id);
            stringBuilder.Append("&scope=bot%20applications.commands");
            if (requiredPermissions != 0)
            {
                stringBuilder.Append("&permissions=");
                stringBuilder.Append((long)requiredPermissions);
            }

            stringBuilder.Append('>');
            return context.RespondAsync(stringBuilder.ToString());
        }

        private static Permissions GetSubcommandsPermissions(IEnumerable<Command> subCommands)
        {
            Permissions permissions = 0;
            foreach (Command subCommand in subCommands)
            {
                if (subCommand.Subcommands.Count != 0)
                {
                    permissions |= GetSubcommandsPermissions(subCommand.Subcommands);
                }

                if (subCommand.Attributes.OfType<RequirePermissionsAttribute>().FirstOrDefault() is RequirePermissionsAttribute permissionsAttribute)
                {
                    permissions |= permissionsAttribute.BotPermissions;
                }
            }

            return permissions;
        }
    }
}
