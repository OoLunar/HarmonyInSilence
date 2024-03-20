using DeepgramSharp;
using DSharpPlus.VoiceLink;

namespace OoLunar.HarmonyInSilence
{
    public sealed record HarmonyUser
    {
        public required VoiceLinkUser VoiceLinkUser { get; init; }
        public required DeepgramLivestreamApi SubtitleConnection { get; init; }
        public required int Channel { get; init; }
    }
}
