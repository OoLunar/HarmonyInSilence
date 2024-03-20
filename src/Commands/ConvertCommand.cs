using System.IO;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Attributes;
using NAudio.Wave;

namespace OoLunar.HarmonyInSilence.Commands
{
    public sealed class ConvertCommand
    {
        [Command("convert"), RequireApplicationOwner]
        [RequireGuild]
        public static async ValueTask ExecuteAsync(CommandContext context, ulong? userId = null)
        {
            userId ??= context.User.Id;
            await context.DeferResponseAsync();

            using FileStream fileStream = File.OpenRead($"tests/{userId}.pcm");
            WaveFileWriter.CreateWaveFile($"tests/{userId}.wav", new RawSourceWaveStream(fileStream, new WaveFormat(48000, 16, 2)));

            await context.RespondAsync($"Converted {userId}.pcm to {userId}.wav");
        }
    }
}
