using DeepgramSharp;
using DSharpPlus.VoiceLink;

namespace OoLunar.HarmonyInSilence
{
    public sealed record HarmonyUser
    {
        public required VoiceLinkConnection VoiceConnection { get; init; }
        public required DeepgramLivestreamApi SubtitleConnection { get; init; }
        public required int Channel { get; init; }
    }
}
