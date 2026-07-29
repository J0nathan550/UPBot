using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

namespace UPBot
{
    /// <summary>
    /// This command is used to configure the bot, so roles and messages can be set for other servers.
    /// author: CPU
    ///
    /// Ported from DSharpPlus's Interactivity extension (WaitForButtonAsync / WaitForMessageAsync),
    /// which Discord.Net has no equivalent for. WaitForButtonAsync/WaitForMessageAsync below are
    /// hand-rolled one-shot event subscriptions. Unlike DSharpPlus, Discord.Net requires every
    /// component interaction to be acknowledged (deferred) quickly, so each loop iteration defers
    /// the button press up front; branches that want to show text on that same interaction use
    /// ModifyOriginalResponseAsync afterwards instead of a second CreateResponseAsync.
    /// </summary>
    public class SlashSetup : InteractionModuleBase<SocketInteractionContext>
    {

        private readonly IEmote ey = new Emoji("✅");
        private readonly IEmote en = new Emoji("❎");
        private readonly IEmote el = new Emoji("↖️");
        private readonly IEmote er = new Emoji("↘️");
        private readonly IEmote ec = new Emoji("❌");
        private static IEmote ok = null;
        private static IEmote ko = null;

        /// <summary>
        /// Discord.Net has no "wait for the next message matching a predicate" helper.
        /// </summary>
        private static Task<SocketMessage> WaitForMessageAsync(Func<SocketMessage, bool> predicate, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<SocketMessage>();
            var client = Utils.GetClient();

            Task Handler(SocketMessage m)
            {
                if (predicate(m)) tcs.TrySetResult(m);
                return Task.CompletedTask;
            }

            client.MessageReceived += Handler;

            _ = Task.Run(async () =>
            {
                await Task.WhenAny(tcs.Task, Task.Delay(timeout));
                client.MessageReceived -= Handler;
                tcs.TrySetResult(null);
            });

            return tcs.Task;
        }

        /// <summary>
        /// Discord.Net has no "wait for a button press on this message" helper either. Returns
        /// the raw component interaction (not auto-deferred) so callers can choose how to
        /// acknowledge it.
        /// </summary>
        private static async Task<SocketMessageComponent> WaitForButtonAsync(IUserMessage message, TimeSpan timeout)
        {
            if (message == null) return null;
            var tcs = new TaskCompletionSource<SocketMessageComponent>();
            var client = Utils.GetClient();

            Task Handler(SocketMessageComponent comp)
            {
                if (comp.Message.Id == message.Id) tcs.TrySetResult(comp);
                return Task.CompletedTask;
            }

            client.ButtonExecuted += Handler;
            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
                return completed == tcs.Task ? tcs.Task.Result : null;
            }
            finally
            {
                client.ButtonExecuted -= Handler;
            }
        }


        [SlashCommand("setup", "Configuration of the features")]
        public async Task SetupCommand([Summary("command", "Show, List, Admins, or Dump")] SetupCommandItem? command = null)
        {
            if (Context.Guild == null)
            {
                await RespondAsync("I cannot be used in Direct Messages.", ephemeral: true);
                return;
            }
            Utils.LogUserCommand(Context);
            SocketGuild g = Context.Guild;
            ulong gid = g.Id;

            if (!Configs.HasAdminRole(gid, ((SocketGuildUser)Context.User).Roles, false))
            {
                await RespondAsync("Only admins can setup the bot.", ephemeral: true);
                return;
            }

            SlashGame.CleanupTicTacToe(); // Remove all games ruiing when starting the setup

            if (command == null || command == SetupCommandItem.Show) await HandleSetupInteractionAsync(gid);
            else if (command == SetupCommandItem.List) await RespondAsync(GenerateSetupList(g, gid));
            else if (command == SetupCommandItem.Save)
            {
                string theList = GenerateSetupList(g, gid);
                string rndName = "SetupList" + DateTime.Now.Second + "Tmp" + DateTime.Now.Millisecond + ".txt";
                await File.WriteAllTextAsync(rndName, theList);
                await using var fs = new FileStream(rndName, FileMode.Open, FileAccess.Read);
                await RespondWithFileAsync(fs, rndName, "Setup List in attachment");
                await Utils.DeleteFileDelayed(30, rndName);
            }
            else await RespondAsync("Wrong choice", ephemeral: true);
        }

        private async Task HandleSetupInteractionAsync(ulong gid)
        {
            ITextChannel channel = Context.Channel as ITextChannel;
            if (ok == null)
            {
                ok = Utils.GetEmoji(EmojiEnum.OK);
                ko = Utils.GetEmoji(EmojiEnum.KO);
            }

            // Basic intro message
            await CreateMainConfigPageAsync();

            IUserMessage msg = await GetOriginalResponseAsync();
            var interRes = await WaitForButtonAsync(msg, TimeSpan.FromMinutes(2));
            await msg.DeleteAsync();
            msg = null;

            while (interRes != null && interRes.Data.CustomId != "idexitconfig")
            {
                await interRes.DeferAsync();
                string cmdId = interRes.Data.CustomId;

                // ******************************************************************** Back *************************************************************************
                if (cmdId == "idback")
                {
                    msg = await FollowMainConfigPageAsync(msg);
                }

                // ***************************************************** DefAdmins ***********************************************************************************
                else if (cmdId == "iddefineadmins")
                {
                    msg = await CreateAdminsInteractionAsync(msg);
                }

                // *********************************************************** DefAdmins.AddRole *******************************************************************************
                else if (cmdId == "idroleadd")
                {
                    if (msg != null) await msg.DeleteAsync();
                    IUserMessage prompt = await channel.SendMessageAsync(Context.User.Mention + ", please mention the roles to add (_type anything else to close_)");
                    var answer = await WaitForMessageAsync(dm => dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(2));
                    if (answer != null)
                    {
                        if (answer.MentionedRoles.Count > 0)
                        {
                            foreach (var dr in answer.MentionedRoles)
                            {
                                if (!Configs.AdminRoles[gid].Contains(dr.Id))
                                {
                                    Configs.AdminRoles[gid].Add(dr.Id);
                                    Database.Add(new AdminRole(gid, dr.Id));
                                }
                            }
                        }
                        else
                        { // Try to find if we have a role with the typed name
                            string rname = answer.Content.Trim();
                            foreach (var role in Context.Guild.Roles)
                            {
                                if (role.Name.Equals(rname, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (!Configs.AdminRoles[gid].Contains(role.Id))
                                    {
                                        Configs.AdminRoles[gid].Add(role.Id);
                                        Database.Add(new AdminRole(gid, role.Id));
                                    }
                                }
                            }
                        }
                    }

                    await prompt.DeleteAsync();
                    msg = await CreateAdminsInteractionAsync(null);
                }

                // *********************************************************** DefAdmins.RemRole *******************************************************************************
                else if (cmdId.Length > 8 && cmdId[..9] == "idrolerem")
                {
                    if (msg != null) await msg.DeleteAsync();
                    if (int.TryParse(cmdId[9..], out int rpos))
                    {
                        ulong rid = Configs.AdminRoles[Context.Guild.Id][rpos];
                        Database.DeleteByKeys<AdminRole>(gid, rid);
                        Configs.AdminRoles[Context.Guild.Id].RemoveAt(rpos);
                    }

                    msg = await CreateAdminsInteractionAsync(null);
                }

                // ************************************************************ DefTracking **************************************************************************
                else if (cmdId == "iddefinetracking")
                {
                    msg = await CreateTrackingInteractionAsync(msg);
                }

                // ************************************************************ DefTracking.Change Channel ************************************************************************
                else if (cmdId == "idchangetrackch")
                {
                    if (msg != null) await msg.DeleteAsync();
                    IUserMessage prompt = await channel.SendMessageAsync(Context.User.Mention + ", please mention the channel (_use: **#**_) as tracking channel\nType _remove_ to remove the tracking channel");
                    var answer = await WaitForMessageAsync(dm => dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id && (dm.MentionedChannels.Count > 0 || dm.Content.Contains("remove", StringComparison.InvariantCultureIgnoreCase)), TimeSpan.FromMinutes(2));
                    if (answer == null || (answer.MentionedChannels.Count == 0 && !answer.Content.Contains("remove", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        await interRes.ModifyOriginalResponseAsync(m => m.Content = "Config timed out");
                        return;
                    }

                    if (answer.MentionedChannels.Count > 0)
                    {
                        SocketTextChannel mentioned = answer.MentionedChannels.First() as SocketTextChannel;
                        if (Configs.TrackChannels[gid] == null)
                        {
                            TrackChannel tc = new();
                            Configs.TrackChannels[gid] = tc;
                            tc.trackJoin = true;
                            tc.trackLeave = true;
                            tc.trackRoles = true;
                            tc.channel = mentioned;
                            tc.Guild = gid;
                            tc.ChannelId = tc.channel.Id;
                        }
                        else
                        {
                            Database.Delete(Configs.TrackChannels[gid]);
                            Configs.TrackChannels[gid].channel = mentioned;
                            Configs.TrackChannels[gid].ChannelId = Configs.TrackChannels[gid].channel.Id;
                        }
                        Database.Add(Configs.TrackChannels[gid]);

                    }
                    else if (answer.Content.Contains("remove", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (Configs.TrackChannels[gid] != null)
                        {
                            Database.Delete(Configs.TrackChannels[gid]);
                            Configs.TrackChannels[gid] = null;
                        }
                    }

                    await prompt.DeleteAsync();
                    msg = await CreateTrackingInteractionAsync(null);
                }

                // ************************************************************ DefTracking.Remove Tracking ************************************************************************
                else if (cmdId == "idremtrackch")
                {
                    if (Configs.TrackChannels[gid] != null)
                    {
                        Database.Delete(Configs.TrackChannels[gid]);
                        Configs.TrackChannels[gid] = null;
                    }

                    msg = await CreateTrackingInteractionAsync(msg);
                }

                // ************************************************************ Alter Tracking Join ************************************************************************
                else if (cmdId == "idaltertrackjoin")
                {
                    AlterTracking(gid, true, false, false);
                    msg = await CreateTrackingInteractionAsync(msg);
                }

                // ************************************************************ Alter Tracking Leave ************************************************************************
                else if (cmdId == "idaltertrackleave")
                {
                    AlterTracking(gid, false, true, false);
                    msg = await CreateTrackingInteractionAsync(msg);
                }

                // ************************************************************ Alter Tracking Roles ************************************************************************
                else if (cmdId == "idaltertrackroles")
                {
                    AlterTracking(gid, false, false, true);
                    msg = await CreateTrackingInteractionAsync(msg);
                }

                // ************************************************************ Weather API Key ************************************************************************
                else if (cmdId == "idweatherapi")
                {
                    msg = await CreateWeatherAPIKeyInteractionAsync(msg);
                }

                else if (cmdId == "idweatherapiadd")
                {
                    await channel.SendMessageAsync($"{Context.User.Mention}, type the API Key to be used");
                    var answer = await WaitForMessageAsync(dm => dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(2));
                    if (answer == null || string.IsNullOrWhiteSpace(answer.Content))
                    {
                        await interRes.ModifyOriginalResponseAsync(m => m.Content = "Config timed out");
                        return;
                    }
                    string key = answer.Content.Trim();
                    Configs.SetWeatherAPIKey(key);

                    msg = await CreateWeatherAPIKeyInteractionAsync(msg);
                }


                // ********* Config Spam Protection ***********************************************************************
                else if (cmdId == "idfeatrespamprotect" || cmdId == "idfeatrespamprotect0" || cmdId == "idfeatrespamprotect1" || cmdId == "idfeatrespamprotect2")
                {
                    SpamProtection sp = Configs.SpamProtections[gid];
                    if (sp == null)
                    {
                        sp = new SpamProtection(gid);
                        Configs.SpamProtections[gid] = sp;
                    }
                    if (cmdId == "idfeatrespamprotect0") sp.protectDiscord = !sp.protectDiscord;
                    if (cmdId == "idfeatrespamprotect1") sp.protectSteam = !sp.protectSteam;
                    if (cmdId == "idfeatrespamprotect2") sp.protectEpic = !sp.protectEpic;
                    Database.Add(sp);
                    msg = await CreateSpamProtectInteractionAsync(msg);
                }
                else if (cmdId == "idfeatrespamprotectbl")
                {
                    msg = await CreateSpamBlackListInteractionAsync(msg);
                }
                else if (cmdId == "idfeatrespamprotectwl")
                {
                    msg = await CreateSpamWhiteListInteractionAsync(msg);
                }
                else if (cmdId.Length > 21 && cmdId[..22] == "idfeatrespamprotectadd")
                { // Ask for the link, clean it up, and add it
                    if (msg != null) await msg.DeleteAsync();
                    bool whitelist = cmdId == "idfeatrespamprotectaddwl";

                    await channel.SendMessageAsync($"{Context.User.Mention}, type the url that should be {(whitelist ? "white listed" : "considered spam")}");
                    var answer = await WaitForMessageAsync(dm => dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(2));
                    if (answer == null || string.IsNullOrWhiteSpace(answer.Content) || !answer.Content.Contains('.'))
                    {
                        await interRes.ModifyOriginalResponseAsync(m => m.Content = "Config timed out");
                        return;
                    }

                    string link = answer.Content.Trim();
                    Regex urlparts = new("[0-9a-z\\.\\-_~]+");
                    foreach (Match m in urlparts.Matches(link))
                    {
                        string url = m.Value.ToLowerInvariant();
                        if (!url.Contains('.')) continue;

                        int leftmostdot = url.LastIndexOf('.');
                        int seconddot = url.LastIndexOf('.', leftmostdot - 1);
                        if (seconddot != -1) url = url[(seconddot + 1)..].Trim();

                        Database.Add(new SpamLink(gid, url, whitelist));
                        bool found = false;
                        var list = whitelist ? Configs.WhiteListLinks : Configs.SpamLinks;
                        foreach (var s in list)
                        {
                            if (s.Equals(url))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            CheckSpam.SpamCheckTimeout = Context.User;
                            if (whitelist)
                            {
                                Configs.WhiteListLinks[gid].Add(url);
                                await channel.SendMessageAsync("New white list URL added.");
                                msg = null;
                            }
                            else
                            {
                                Configs.SpamLinks[gid].Add(url);
                                await channel.SendMessageAsync("New spam URL added.");
                                msg = null;
                            }
                        }
                    }
                    msg = await CreateSpamProtectInteractionAsync(msg);
                }
                else if (cmdId.Length > 27 && cmdId[..27] == "idfeatrespamprotectremovebl")
                {
                    if (int.TryParse(cmdId[27..], out int num))
                    {
                        string link = Configs.SpamLinks[gid][num];
                        Configs.SpamLinks[gid].RemoveAt(num);
                        Database.DeleteByKeys<SpamLink>(gid, link);
                    }
                    msg = await CreateSpamProtectInteractionAsync(msg);
                }
                else if (cmdId.Length > 27 && cmdId[..27] == "idfeatrespamprotectremovewl")
                {
                    if (int.TryParse(cmdId[27..], out int num))
                    {
                        string link = Configs.WhiteListLinks[gid][num];
                        Configs.WhiteListLinks[gid].RemoveAt(num);
                        Database.DeleteByKeys<SpamLink>(gid, link);
                    }
                    msg = await CreateSpamProtectInteractionAsync(msg);
                }
                else if (cmdId == "idbackspam")
                {
                    msg = await CreateSpamProtectInteractionAsync(msg);
                }

                // ***************************************************** UNKNOWN ***********************************************************************************
                else
                {
                    Utils.Log("Unknown interaction result: " + cmdId, Context.Guild.Name);
                }
                interRes = await WaitForButtonAsync(msg, TimeSpan.FromMinutes(2));
            }
            if (interRes == null) { if (msg != null) await msg.DeleteAsync(); } // Expired
            else await interRes.ModifyOriginalResponseAsync(m => m.Content = "Config completed");

        }

        private static string GenerateSetupList(SocketGuild g, ulong gid)
        { // list

            string msg = "Setup list for Discord Server " + g.Name + "\n";
            string part = "";
            // Admins ******************************************************
            if (Configs.AdminRoles[gid].Count == 0) msg += "**AdminRoles**: _no roles defined. Owner and roles with Admin flag will be considered bot Admins_\n";
            else
            {
                foreach (var rid in Configs.AdminRoles[gid])
                {
                    SocketRole r = g.GetRole(rid);
                    if (r != null) part += r.Name + ", ";
                }
                if (part.Length == 0) msg += "**AdminRoles**: _no roles defined. Owner and roles with Admin flag will be considered bot Admins_\n";
                else msg += "**AdminRoles**: " + part[..^2] + "\n";
            }

            // TrackingChannel ******************************************************
            if (Configs.TrackChannels[gid] == null) msg += "**TrackingChannel**: _no tracking channel defined_\n";
            else
            {
                msg += "**TrackingChannel**: " + Configs.TrackChannels[gid].channel.Mention + " for ";
                if (Configs.TrackChannels[gid].trackJoin || Configs.TrackChannels[gid].trackLeave || Configs.TrackChannels[gid].trackRoles)
                {
                    if (Configs.TrackChannels[gid].trackJoin) msg += "_Join_ ";
                    if (Configs.TrackChannels[gid].trackLeave) msg += "_Leave_ ";
                    if (Configs.TrackChannels[gid].trackRoles) msg += "_Roles_ ";
                }
                else msg += "nothing";
                msg += "\n";
            }

            // SpamProtection ******************************************************
            SpamProtection sp = Configs.SpamProtections[gid];
            if (sp == null) msg += "**Spam Protection**: _not defined (disabled by default)_\n";
            else if (sp.protectDiscord)
            {
                if (sp.protectSteam)
                {
                    if (sp.protectEpic)
                    {
                        msg += "**Spam Protection**: enabled for _Discord_, _Steam_, and _Epic_\n";
                    }
                    else
                    {
                        msg += "**Spam Protection**: enabled for _Discord_ and _Steam_\n";
                    }
                }
                else
                {
                    if (sp.protectEpic)
                    {
                        msg += "**Spam Protection**: enabled for _Discord_ and _Epic_\n";
                    }
                    else
                    {
                        msg += "**Spam Protection**: enabled for _Discord_ only\n";
                    }
                }
            }
            else
            {
                if (sp.protectSteam)
                {
                    if (sp.protectEpic)
                    {
                        msg += "**Spam Protection**: enabled for _Steam_ and _Epic_\n";
                    }
                    else
                    {
                        msg += "**Spam Protection**: enabled for _Steam_ only\n";
                    }
                }
                else
                {
                    if (sp.protectEpic)
                    {
                        msg += "**Spam Protection**: enabled for _Epic_ only\n";
                    }
                    else
                    {
                        msg += "**Spam Protection**: _disabled_\n";
                    }
                }
            }
            if (Configs.SpamLinks.TryGetValue(gid, out List<string> value) && value.Count > 0)
            {
                msg += "**Specific spam links**: ";
                bool first = true;
                foreach (string sl in value)
                {
                    if (!first)
                    {
                        msg += ", ";
                        first = false;
                    }
                    msg += sl;
                }
            }

            return msg;
        }

        public enum SetupCommandItem
        {
            [ChoiceDisplay("Show")] Show = 0,
            [ChoiceDisplay("List")] List = 1,
            [ChoiceDisplay("Save")] Save = 2,
            [ChoiceDisplay("Admins")] Admins = 3
        }

        private static void AlterTracking(ulong gid, bool j, bool l, bool r)
        {
            TrackChannel tc = Configs.TrackChannels[gid];
            if (j) tc.trackJoin = !tc.trackJoin;
            if (l) tc.trackLeave = !tc.trackLeave;
            if (r) tc.trackRoles = !tc.trackRoles;
            Database.Update(tc);
        }

        private SocketGuildUser Invoker => (SocketGuildUser)Context.User;

        private async Task CreateMainConfigPageAsync()
        {
            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            eb.Description = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**";
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            //- Set tracking
            //- Set Admins
            //- Spam Protection
            //- Weather API Key
            SpamProtection sp = Configs.SpamProtections[Context.Guild.Id];
            bool spdisabled = sp == null || (!sp.protectDiscord && !sp.protectSteam && !sp.protectEpic);

            ComponentBuilder cb = new();
            cb.WithButton("Define Admins", "iddefineadmins", ButtonStyle.Primary, er);
            cb.WithButton("Define Tracking channel", "iddefinetracking", ButtonStyle.Primary, er);
            cb.WithButton("Spam Protection", "idfeatrespamprotect", spdisabled ? ButtonStyle.Secondary : ButtonStyle.Primary, er);
            cb.WithButton("Weather API Key", "idweatherapi", ButtonStyle.Primary, er);

            //-Exit
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger);

            await RespondAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> FollowMainConfigPageAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            eb.Description = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**";
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            //- Set tracking
            //- Set Admins
            //- Spam Protection
            SpamProtection sp = Configs.SpamProtections[Context.Guild.Id];
            bool spdisabled = sp == null || (!sp.protectDiscord && !sp.protectSteam && !sp.protectEpic);

            ComponentBuilder cb = new();
            cb.WithButton("Define Admins", "iddefineadmins", ButtonStyle.Primary, er);
            cb.WithButton("Define Tracking channel", "iddefinetracking", ButtonStyle.Primary, er);
            cb.WithButton("Spam Protection", "idfeatrespamprotect", spdisabled ? ButtonStyle.Secondary : ButtonStyle.Primary, er);

            //-Exit
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> CreateAdminsInteractionAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration - Admin roles"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            string desc = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**\n\n\n" +
              "Current server roles that are considered bot administrators:\n";

            // List admin roles
            if (Configs.AdminRoles[Context.Guild.Id].Count == 0) desc += "_**No admin roles defined.** Owner and server Admins will be used_";
            else
            {
                List<ulong> roles = Configs.AdminRoles[Context.Guild.Id];
                bool one = false;
                foreach (ulong role in roles)
                {
                    SocketRole dr = Context.Guild.GetRole(role);
                    if (dr != null)
                    {
                        desc += dr.Mention + ", ";
                        one = true;
                    }
                }
                if (one) desc = desc[..^2];
                else desc += "_**No admin roles defined.** Owner and server Admins will be used_";
            }
            eb.Description = desc;
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            ComponentBuilder cb = new();

            // - Define roles
            cb.WithButton("Add roles", "idroleadd", ButtonStyle.Primary, ok);

            // - Remove roles
            int num = 0;
            foreach (ulong rid in Configs.AdminRoles[Context.Guild.Id])
            {
                SocketRole role = Context.Guild.GetRole(rid);
                if (role == null)
                {
                    Database.DeleteByKeys<AdminRole>(Context.Guild.Id, rid);
                    continue;
                }
                cb.WithButton("Remove " + role.Name, "idrolerem" + num, ButtonStyle.Primary, ko);
                num++;
            }

            // - Exit
            // - Back
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);
            cb.WithButton("Back", "idback", ButtonStyle.Secondary, el);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> CreateTrackingInteractionAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            TrackChannel tc = Configs.TrackChannels[Context.Guild.Id];

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration - Tracking channel"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            string desc = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**\n\n\n";
            if (tc == null) desc += "_**No tracking channel defined.**_";
            else
            {
                if (tc.channel == null) desc += "_**No tracking channel defined.**_";
                else desc += "_**Tracking channel:** " + tc.channel.Mention + "_";
            }
            eb.Description = desc;
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            ComponentBuilder cb = new();

            // - Change channel
            cb.WithButton("Change channel", "idchangetrackch", ButtonStyle.Primary, ok);
            if (Configs.TrackChannels[Context.Guild.Id] != null)
                cb.WithButton("Remove channel", "idremtrackch", ButtonStyle.Primary, ko);

            // - Actions to track
            if (tc != null)
            {
                cb.WithButton(tc.trackJoin ? "Track Join" : "Track Joint", "idaltertrackjoin", tc.trackJoin ? ButtonStyle.Primary : ButtonStyle.Secondary, tc.trackJoin ? ey : en);
                cb.WithButton("Track Leave", "idaltertrackleave", tc.trackLeave ? ButtonStyle.Primary : ButtonStyle.Secondary, tc.trackLeave ? ey : en);
                cb.WithButton("Track Roles", "idaltertrackroles", tc.trackRoles ? ButtonStyle.Primary : ButtonStyle.Secondary, tc.trackRoles ? ey : en);
            }

            // - Exit
            // - Back
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);
            cb.WithButton("Back", "idback", ButtonStyle.Secondary, el);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> CreateSpamProtectInteractionAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration - Spam Protection"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            SpamProtection sp = Configs.SpamProtections[Context.Guild.Id];
            bool edisc = sp != null && sp.protectDiscord;
            bool esteam = sp != null && sp.protectSteam;
            bool eepic = sp != null && sp.protectEpic;
            eb.Description = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**\n\n" +
              "The **Scam Protection** feature watches messages for suspicious repeat-content and image-spam patterns.\n" +
              "Custom black/white list entries are stored in the SQLite database and can be managed below.\n\n**Spam Protection** for\n";
            eb.Description += "**Discord Nitro** feature is " + (edisc ? "_Enabled_" : "_Disabled_") + " (_recommended!_)\n";
            eb.Description += "**Steam** feature is " + (esteam ? "_Enabled_" : "_Disabled_") + "\n";
            eb.Description += "**Epic Game Store** feature is " + (eepic ? "_Enabled_" : "_Disabled_") + "\n";
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            ComponentBuilder cb = new();
            cb.WithButton("Discord Nitro", "idfeatrespamprotect0", edisc ? ButtonStyle.Success : ButtonStyle.Danger, edisc ? ey : en);
            cb.WithButton("Steam", "idfeatrespamprotect1", esteam ? ButtonStyle.Success : ButtonStyle.Danger, esteam ? ey : en);
            cb.WithButton("Epic", "idfeatrespamprotect2", eepic ? ButtonStyle.Success : ButtonStyle.Danger, eepic ? ey : en);

            cb.WithButton("Manage Black List", "idfeatrespamprotectbl", ButtonStyle.Success, er);
            cb.WithButton("Manage White List", "idfeatrespamprotectwl", ButtonStyle.Success, er);

            // - Exit
            // - Back
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);
            cb.WithButton("Back to Main", "idback", ButtonStyle.Secondary, el);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> CreateSpamWhiteListInteractionAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration - Spam Protection"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            eb.Description = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**\n\n" +
              "White List of links for the **Scam Protection**, these links will always be allowed.\n" +
              "Add with the button a link that will always be accepted in all posted messages.\n" +
              "Click on an existing link button to remove it from the white list.\n" +
              "Entries are stored in SQLite for this server.";
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            ComponentBuilder cb = new();
            cb.WithButton("Add custom non spam url", "idfeatrespamprotectaddwl", ButtonStyle.Success, ok);

            // List all custom spam links
            int counter = 0;
            foreach (string sl in Configs.WhiteListLinks[Context.Guild.Id])
            {
                cb.WithButton(sl, $"idfeatrespamprotectremovewl{counter}", ButtonStyle.Success, ko);
                counter++;
            }

            // - Exit
            // - Back
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);
            cb.WithButton("Back to Main", "idback", ButtonStyle.Secondary, el);
            cb.WithButton("Back to Spam Protection", "idbackspam", ButtonStyle.Secondary, el);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> CreateSpamBlackListInteractionAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration - Spam Protection"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            eb.Description = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**\n\n" +
              "Black List of links for the **Scam Protection**.\n" +
              "Add with the button a link that will be banned from all messages posted.\n" +
              "Click on an existing link button to remove it from the black list.\n" +
              "Entries are stored in SQLite for this server.";
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            ComponentBuilder cb = new();
            cb.WithButton("Add custom spam url", "idfeatrespamprotectaddbl", ButtonStyle.Success, ok);

            // List all custom spam links
            int counter = 0;
            foreach (string sl in Configs.SpamLinks[Context.Guild.Id])
            {
                cb.WithButton(sl, $"idfeatrespamprotectremovebl{counter}", ButtonStyle.Success, ko);
                counter++;
            }

            // - Exit
            // - Back
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);
            cb.WithButton("Back to Main", "idback", ButtonStyle.Secondary, el);
            cb.WithButton("Back to Spam Protection", "idbackspam", ButtonStyle.Secondary, el);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }

        private async Task<IUserMessage> CreateWeatherAPIKeyInteractionAsync(IUserMessage prevMsg)
        {
            if (prevMsg != null) await prevMsg.DeleteAsync();

            EmbedBuilder eb = new()
            {
                Title = "UPBot Configuration - Weather API Key"
            };
            eb.WithThumbnailUrl(Context.Guild.IconUrl);
            eb.Description = "Configuration of the UP Bot for the Discord Server **" + Context.Guild.Name + "**\n\n" +
              "**Weather API Key**\n" +
              "Get a an API Key from the site https://www.weatherapi.com/ and type its value here.\n" +
              "Current key: " + (string.IsNullOrWhiteSpace(Configs.WeatherAPIKey) ? "_undefined_" : Configs.WeatherAPIKey);
            eb.WithImageUrl(Context.Guild.BannerUrl);
            eb.WithFooter("Member that started the configuration is: " + Invoker.DisplayName, Invoker.GetDisplayAvatarUrl() ?? Invoker.GetDefaultAvatarUrl());

            ComponentBuilder cb = new();
            cb.WithButton("Change API Key", "idweatherapiadd", ButtonStyle.Success, ok);

            // - Exit
            // - Back
            cb.WithButton("Exit", "idexitconfig", ButtonStyle.Danger, ec);
            cb.WithButton("Back to Main", "idback", ButtonStyle.Secondary, el);

            ITextChannel channel = Context.Channel as ITextChannel;
            return await channel.SendMessageAsync(embed: eb.Build(), components: cb.Build());
        }
    }
}
