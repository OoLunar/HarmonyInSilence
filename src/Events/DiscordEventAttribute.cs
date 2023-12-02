using System;
using DSharpPlus;

namespace @RepositoryOwner.@RepositoryName.Events
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class DiscordEventAttribute(DiscordIntents intents) : Attribute
    {
        public DiscordIntents Intents { get; init; } = intents;
    }
}
