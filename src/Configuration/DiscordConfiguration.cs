namespace OoLunar.HarmonyInSilence.Configuration
{
    public sealed record DiscordConfiguration
    {
        public required string? Token { get; init; }
        public string Prefix { get; init; } = "h!";
        public ulong GuildId { get; init; }
    }
}