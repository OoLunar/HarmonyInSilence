using System.IO;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees.Attributes;
using NAudio.Wave;
using OoLunar.HarmonyInSilence.Configuration;

namespace OoLunar.HarmonyInSilence.Commands
{
    public sealed class ConvertCommand
    {
        private readonly HarmonyConfiguration _harmonyConfiguration;
        public ConvertCommand(HarmonyConfiguration harmonyConfiguration) => _harmonyConfiguration = harmonyConfiguration;

        [Command("convert"), RequireApplicationOwner, RequireGuild]
        public async ValueTask ExecuteAsync(CommandContext context)
        {
            await context.DeferResponseAsync();

            using FileStream fileStream = File.OpenRead($"tests/test.pcm");
            WaveFileWriter.CreateWaveFile($"tests/test.wav", new RawSourceWaveStream(fileStream, new WaveFormat(48000, 16, _harmonyConfiguration.Deepgram.MaxChannelCount)));

            await context.RespondAsync($"Converted test.pcm to test.wav");
        }
    }
}
