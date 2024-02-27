namespace OoLunar.HarmonyInSilence.Configuration
{
    public sealed record HarmonyConfiguration
    {
        public required DiscordConfiguration? Discord { get; init; }
        public required DeepgramConfiguration? Deepgram { get; init; }
        public LoggerConfiguration Logger { get; init; } = new();
    }
}