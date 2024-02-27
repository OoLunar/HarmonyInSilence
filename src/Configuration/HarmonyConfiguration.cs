namespace OoLunar.HarmonyInSilence.Configuration
{
    public sealed record HarmonyConfiguration
    {
        public required DiscordConfiguration? Discord { get; set; }
        public required DeepgramConfiguration? Deepgram { get; set; }
        public LoggerConfiguration Logger { get; set; } = new();
    }
}