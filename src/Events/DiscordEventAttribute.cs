using System;

namespace OoLunar.HarmonyInSilence.Events
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class DiscordEventAttribute : Attribute;
}
