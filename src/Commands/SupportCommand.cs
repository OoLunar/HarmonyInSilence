using System.ComponentModel;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Attributes;
using OoLunar.HarmonyInSilence.Configuration;

namespace OoLunar.HarmonyInSilence.Commands
{
    public sealed class SupportCommand
    {
        private readonly DiscordConfiguration _configuration;

        public SupportCommand(HarmonyConfiguration configuration) => _configuration = configuration.Discord;

        [Command("support"), Description("Would you like assistance with the bot?")]
        public async ValueTask ExecuteAsync(CommandContext context) => await context.RespondAsync($"If you need help, you're always welcome to join the support server: {_configuration.SupportServerInvite}");
    }
}
