using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DeepgramSharp;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.VoiceLink;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OoLunar.HarmonyInSilence.Configuration;
using OoLunar.HarmonyInSilence.Events;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using DSharpPlusDiscordConfiguration = DSharpPlus.DiscordConfiguration;
using SerilogLoggerConfiguration = Serilog.LoggerConfiguration;

namespace OoLunar.HarmonyInSilence
{
    public sealed class Program
    {
        public static async Task Main(string[] args)
        {
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(serviceProvider =>
            {
                ConfigurationBuilder configurationBuilder = new();
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddJsonFile("config.json", true, true);
#if DEBUG
                configurationBuilder.AddJsonFile("config.debug.json", true, true);
#endif
                configurationBuilder.AddEnvironmentVariables("HarmonyInSilence__");
                configurationBuilder.AddCommandLine(args);

                IConfiguration configuration = configurationBuilder.Build();
                HarmonyConfiguration? harmonyConfiguration = configuration.Get<HarmonyConfiguration>();
                if (harmonyConfiguration is null)
                {
                    Console.WriteLine("No configuration found! Please modify the config file, set environment variables or pass command line arguments. Exiting...");
                    Environment.Exit(1);
                }

                return harmonyConfiguration;
            });

            services.AddLogging(logging =>
            {
                IServiceProvider serviceProvider = logging.Services.BuildServiceProvider();
                HarmonyConfiguration harmonyConfiguration = serviceProvider.GetRequiredService<HarmonyConfiguration>();
                SerilogLoggerConfiguration serilogLoggerConfiguration = new();
                serilogLoggerConfiguration.MinimumLevel.Is(harmonyConfiguration.Logger.LogLevel);
                serilogLoggerConfiguration.WriteTo.Console(
                    formatProvider: CultureInfo.InvariantCulture,
                    outputTemplate: harmonyConfiguration.Logger.Format,
                    theme: AnsiConsoleTheme.Code
                );

                serilogLoggerConfiguration.WriteTo.File(
                    formatProvider: CultureInfo.InvariantCulture,
                    path: $"{harmonyConfiguration.Logger.Path}/{harmonyConfiguration.Logger.FileName}.log",
                    rollingInterval: harmonyConfiguration.Logger.RollingInterval,
                    outputTemplate: harmonyConfiguration.Logger.Format
                );

                // Sometimes the user/dev needs more or less information about a speific part of the bot
                // so we allow them to override the log level for a specific namespace.
                if (harmonyConfiguration.Logger.Overrides.Count > 0)
                {
                    foreach ((string key, LogEventLevel value) in harmonyConfiguration.Logger.Overrides)
                    {
                        serilogLoggerConfiguration.MinimumLevel.Override(key, value);
                    }
                }

                logging.AddSerilog(serilogLoggerConfiguration.CreateLogger());
            });

            services.AddSingleton(serviceProvider =>
            {
                HarmonyConfiguration harmonyConfiguration = serviceProvider.GetRequiredService<HarmonyConfiguration>();
                if (harmonyConfiguration.Deepgram is null || string.IsNullOrWhiteSpace(harmonyConfiguration.Deepgram.Token))
                {
                    serviceProvider.GetRequiredService<ILogger<Program>>().LogCritical("Deepgram token is not set! Exiting...");
                    Environment.Exit(1);
                }

                return new DeepgramClient(harmonyConfiguration.Deepgram.Token, serviceProvider.GetRequiredService<ILogger<DeepgramClient>>());
            });

            services.AddSingleton<HarmonyUserMapper>();
            services.AddSingleton(serviceProvider =>
            {
                DiscordEventManager eventManager = new(serviceProvider);
                eventManager.GatherEventHandlers(typeof(Program).Assembly);
                return eventManager;
            });

            services.AddSingleton(serviceProvider =>
            {
                HarmonyConfiguration harmonyConfiguration = serviceProvider.GetRequiredService<HarmonyConfiguration>();
                if (harmonyConfiguration.Discord is null || string.IsNullOrWhiteSpace(harmonyConfiguration.Discord.Token))
                {
                    serviceProvider.GetRequiredService<ILogger<Program>>().LogCritical("Discord token is not set! Exiting...");
                    Environment.Exit(1);
                }

                DiscordShardedClient discordClient = new(new DSharpPlusDiscordConfiguration
                {
                    Token = harmonyConfiguration.Discord.Token,
                    Intents = TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents | DiscordIntents.GuildVoiceStates | DiscordIntents.MessageContents,
                    LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),
                });

                return discordClient;
            });

            // Almost start the program
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            HarmonyConfiguration harmonyConfiguration = serviceProvider.GetRequiredService<HarmonyConfiguration>();
            DiscordShardedClient discordClient = serviceProvider.GetRequiredService<DiscordShardedClient>();
            DiscordEventManager eventManager = serviceProvider.GetRequiredService<DiscordEventManager>();

            // Register extensions here since these involve asynchronous operations
            IReadOnlyDictionary<int, CommandsExtension> commandsExtensions = await discordClient.UseCommandsAsync(new CommandsConfiguration()
            {
                ServiceProvider = serviceProvider,
                DebugGuildId = harmonyConfiguration.Discord.GuildId
            });

            // Iterate through each Discord shard
            foreach (CommandsExtension commandsExtension in commandsExtensions.Values)
            {
                // Add all commands by scanning the current assembly
                commandsExtension.AddCommands(typeof(Program).Assembly);
                TextCommandProcessor textCommandProcessor = new(new()
                {
                    PrefixResolver = new DefaultPrefixResolver(harmonyConfiguration.Discord.Prefix).ResolvePrefixAsync
                });

                // Add text commands (h!ping) and slash commands (/ping)
                await commandsExtension.AddProcessorsAsync(textCommandProcessor, new SlashCommandProcessor());
                eventManager.RegisterEventHandlers(commandsExtension);
            }

            // Setup the extension that connects to the Discord voice gateway (voice channels)
            IReadOnlyDictionary<int, VoiceLinkExtension> voiceLinkExtensions = await discordClient.UseVoiceLinkAsync(new VoiceLinkConfiguration()
            {
                ServiceCollection = services
            });

            // Iterate through each Discord shard
            foreach (VoiceLinkExtension voiceLinkExtension in voiceLinkExtensions.Values)
            {
                eventManager.RegisterEventHandlers(voiceLinkExtension);
            }

            // Register event handlers for the Discord Client itself
            eventManager.RegisterEventHandlers(discordClient);

            // Connect the bot to the Discord gateway.
            await discordClient.StartAsync();

            // Start listening for commands.
            await Task.Delay(-1);
        }
    }
}
