namespace OoLunar.HarmonyInSilence.Configuration
{
    public sealed record DeepgramConfiguration
    {
        public required string? Token { get; init; }
        public int MaxChannelCount { get; init; } = 255;
    }
}
