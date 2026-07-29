using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UPBot.DiscordRPC;
using UPBot.UPBot_Code;

namespace UPBot
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Title = $"UPBot {Utils.GetVersion()}";
                Console.ForegroundColor = ConsoleColor.Red;
                Utils.Log("You have to specify the bot token as first parameter and the logs path as second parameter!", null);
                return;
            }
            Utils.LogsFolder = args[1];
            Console.ForegroundColor = ConsoleColor.Green;
            Utils.Log("Log Started. Woho.", null);
            Console.ForegroundColor = ConsoleColor.White;

            try
            {
                MainAsync(args[0]).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Utils.Log("Exit for critical failure", null);
                Console.ForegroundColor = ConsoleColor.White;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Utils.Log("Exit by error: " + ex.Message, null);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static readonly CancellationTokenSource exitToken = new();
        private static bool eventsRegistered = false; // guards against re-subscribing handlers on Ready re-fires (reconnects)
        private static InteractionService interactions;
        private static IServiceProvider services;

        private static async Task MainAsync(string token)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Utils.Log("Init Main", null);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Utils.Log("Version: " + Utils.GetVersion(), null);
                Console.ForegroundColor = ConsoleColor.White;

                var client = new DiscordSocketClient(new DiscordSocketConfig()
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
                    AlwaysDownloadUsers = true
                });

                Utils.Log("Utils.InitClient", null);
                Utils.InitClient(client);

                Database.InitDb([
          typeof(SpamProtection), typeof(Timezone), typeof(AdminRole), typeof(TrackChannel), typeof(TagBase), typeof(SpamLink), typeof(WeatherAPIKey)
        ]);
                Utils.Log("Database.InitDb", null);

                // Interaction (slash) commands
                Utils.Log("SlashCommands", null);
                interactions = new InteractionService(client.Rest);
                services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();

                await interactions.AddModuleAsync<SlashVersion>(services);
                await interactions.AddModuleAsync<SlashPing>(services);
                await interactions.AddModuleAsync<SlashRefactor>(services);
                await interactions.AddModuleAsync<SlashDelete>(services);
                await interactions.AddModuleAsync<SlashWhoIs>(services);
                await interactions.AddModuleAsync<SlashGame>(services);
                await interactions.AddModuleAsync<SlashTags>(services);
                await interactions.AddModuleAsync<SlashTagsEdit>(services);
                await interactions.AddModuleAsync<SlashStats>(services);
                await interactions.AddModuleAsync<SlashTimezone>(services);
                await interactions.AddModuleAsync<SlashLogs>(services);
                await interactions.AddModuleAsync<SlashSetup>(services);
                await interactions.AddModuleAsync<Weather>(services);

                client.InteractionCreated += async interaction =>
                {
                    var ctx = new SocketInteractionContext(client, interaction);
                    await interactions.ExecuteCommandAsync(ctx, services);
                };

                Utils.Log("Connecting to discord...", null);
                client.Ready += Discord_Ready;

                await Task.Delay(50);
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();

                // Check for a while if we have any guild
                int t = 0;
                while (Utils.GetClient() == null)
                { // 10 secs max for client
                    await Task.Delay(1000);
                    if (t++ > 10)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Utils.Log("CRITICAL ERROR: We are not connecting! (no client)", null);
                        Console.WriteLine("CRITICAL ERROR: No discord client");
                        return;
                    }
                }

                // 10 secs max for guilds
                t = 0;
                while (Utils.GetClient().Guilds == null)
                {
                    await Task.Delay(1000);
                    if (t++ > 10)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Utils.Log("CRITICAL ERROR: We are not connecting! (no guilds)", null);
                        Console.WriteLine("CRITICAL ERROR: No guilds available");
                        return;
                    }
                }

                // 30 secs max for guilds count
                t = 0;
                while (Utils.GetClient().Guilds.Count == 0)
                {
                    await Task.Delay(1000);
                    if (t++ > 30)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Utils.Log("CRITICAL ERROR: We are not connecting! (guilds count is zero)", null);
                        Console.WriteLine("CRITICAL ERROR: The bot seems to be in no guild");
                        return;
                    }
                }


            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Utils.Log("with exception: " + ex.Message, null);
                Console.ForegroundColor = ConsoleColor.White;
            }

            // Wait forever
            await Task.Delay(-1, exitToken.Token);
        }

        private static async Task Discord_Ready()
        {
            DiscordSocketClient client = Utils.GetClient();
            Console.ForegroundColor = ConsoleColor.Green;
            Utils.Log("connected", null);
            Console.ForegroundColor = ConsoleColor.White;
            Utils.Log("Logging [re]Started at: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:dd") + " --------------------------------", null);

            // Register slash commands globally now that we're connected
            try
            {
                await interactions.RegisterCommandsGloballyAsync();
            }
            catch (Exception ex)
            {
                Utils.Log("Failed to register slash commands: " + ex.Message, null);
            }

            await Task.Delay(500);
            Console.ForegroundColor = ConsoleColor.Green;
            Utils.Log("Setup complete, waiting guilds to be ready", null);
            Console.ForegroundColor = ConsoleColor.White;
            _ = WaitForGuildsTask(client);
        }

        private static async Task WaitForGuildsTask(DiscordSocketClient client)
        {
            Dictionary<ulong, bool> guilds = [];
            int toGet = client.Guilds.Count;
            foreach (var g in client.Guilds)
                guilds[g.Id] = false;

            int times = 0;
            bool cleanOldGuilds = true;
            while (true)
            {
                times++;
                foreach (var g in client.Guilds)
                {
                    guilds[g.Id] = g.IsConnected && !string.IsNullOrEmpty(g.Name);
                }
                int num = 0;
                foreach (bool b in guilds.Values) if (b) num++;

                if (num == toGet)
                {
                    cleanOldGuilds = false;
                    break;
                }
                await Task.Delay(500);
                if (times % 21 == 20)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Utils.Log("Tried " + times + " got only " + num + "/" + toGet, null);
                    Console.ForegroundColor = ConsoleColor.White;
                }

                if (times > 300)
                {
                    if (num > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Utils.Log("Stopping the wait, got only " + num + " over " + toGet, null);
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Utils.Log("[CRITICAL] Stopping. We cannot find any valid Discord server.", null);
                        Console.ForegroundColor = ConsoleColor.White;
                        exitToken.Cancel();
                        return;
                    }
                }
            }
            // Remove guild that are no more valid
            if (cleanOldGuilds)
            {
                foreach (var g in client.Guilds)
                {
                    if (!g.IsConnected || string.IsNullOrEmpty(g.Name))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Utils.Log("Leaving guild with id: " + g.Id, null);
                        try
                        {
                            _ = g.LeaveAsync();
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Utils.Log("Error in Leaving guild: " + e.Message, null);
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Utils.Log($"Got all guilds after '{times}'", null);
            Console.ForegroundColor = ConsoleColor.White;
            foreach (var g in client.Guilds)
            {
                if (!g.IsConnected)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Utils.Log($">> {g.Name} (NOT WORKING)", null);
                }
                else
                    Utils.Log($">> {g.Name}", null);
                Console.ForegroundColor = ConsoleColor.White;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Utils.Log("LoadingParams", null);
            Configs.LoadParams();
            Console.ForegroundColor = ConsoleColor.White;

            if (!eventsRegistered)
            {
                eventsRegistered = true;

                Console.ForegroundColor = ConsoleColor.Green;
                Utils.Log("Adding action events", null);
                client.UserJoined += MembersTracking.DiscordMemberAdded;
                client.UserLeft += MembersTracking.DiscordMemberRemoved;
                client.GuildMemberUpdated += MembersTracking.DiscordMemberUpdated;

                client.MessageReceived += async (m) => { await CheckSpam.CheckMessageCreate(m); };
                client.MessageUpdated += async (before, after, channel) => { await CheckSpam.CheckMessageUpdate(before, after, channel); };
                Console.ForegroundColor = ConsoleColor.White;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Utils.Log("Tracking", null);
                Console.ForegroundColor = ConsoleColor.White;

                Utils.Log("DiscordRichPresence", null);
                DiscordStatus.Start(client);

                client.JoinedGuild += Configs.NewGuildAdded;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Utils.Log("Ready re-fired (reconnect) - skipping event re-registration to avoid duplicate handlers", null);
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Utils.Log("--->>> Bot ready <<<---", null);
            Console.ForegroundColor = ConsoleColor.White;

        }
    }
}
