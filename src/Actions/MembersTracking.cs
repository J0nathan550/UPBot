using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

namespace UPBot
{
    public class MembersTracking
    {
        private static Dictionary<ulong, DateTime> tracking = null; // Use one from COnfig, add nonserializable datetime if we need one

        public static async Task DiscordMemberRemoved(SocketGuild guild, SocketUser user)
        {
            try
            {
                TrackChannel trackChannel = Configs.TrackChannels[guild.Id];
                if (trackChannel == null || trackChannel.channel == null || !trackChannel.trackLeave) return;
                tracking ??= [];

                SocketGuildUser member = user as SocketGuildUser;
                DateTime joinedAt = member?.JoinedAt?.DateTime ?? DateTime.Now;
                string displayName = member?.DisplayName ?? user.Username;
                int daysJ = (int)(DateTime.Now - joinedAt).TotalDays;
                if (daysJ > 10000) daysJ = -1; // User is probably destroyed. So the value will be not valid

                if (tracking.ContainsKey(user.Id) || (daysJ >= 0 && daysJ < 2))
                {
                    tracking.Remove(user.Id);
                    string msg = "User " + displayName + " did a kiss and go. (" + guild.MemberCount + " members total)";
                    await trackChannel.channel.SendMessageAsync(msg);
                    Utils.Log(msg, guild.Name);
                }
                else
                {
                    string msgC = daysJ >= 0
                        ? Utils.GetEmojiSnowflakeID(EmojiEnum.KO) + "  User " + user.Mention + " (" + displayName + ") left on " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + " after " + daysJ + " days (" + guild.MemberCount + " members total)"
                        : Utils.GetEmojiSnowflakeID(EmojiEnum.KO) + "  User " + user.Mention + " (" + displayName + ") left on " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + " (" + guild.MemberCount + " members total)";
                    string msgL = "- User " + displayName + " left on " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + " (" + guild.MemberCount + " members total)";
                    await trackChannel.channel.SendMessageAsync(msgC);
                    Utils.Log(msgL, guild.Name);
                }
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                Utils.Log("Error in DiscordMemberRemoved: " + ex.Message, guild.Name);
            }

            await Task.Delay(50);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public static async Task DiscordMemberAdded(SocketGuildUser member)
        {
            try
            {
                TrackChannel trackChannel = Configs.TrackChannels[member.Guild.Id];
                if (trackChannel == null || trackChannel.channel == null || !trackChannel.trackJoin) return;
                tracking ??= [];

                tracking[member.Id] = DateTime.Now;
                _ = SomethingAsync(trackChannel.channel, member.Id, member.DisplayName, member.Mention, member.Guild.MemberCount);
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                Utils.Log("Error in DiscordMemberAdded: " + ex.Message, member.Guild.Name);
            }
            await Task.Delay(10);
        }
#pragma warning restore IDE0060 // Remove unused parameter

        public static async Task DiscordMemberUpdated(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
        {
            SocketGuild guild = after.Guild;
            try
            {
                TrackChannel trackChannel = Configs.TrackChannels[guild.Id];
                if (trackChannel == null || trackChannel.channel == null || !trackChannel.trackRoles) return;
                tracking ??= [];

                SocketGuildUser beforeUser = before.HasValue ? before.Value : null;
                IReadOnlyCollection<SocketRole> rolesBefore = beforeUser?.Roles ?? new List<SocketRole>();
                IReadOnlyCollection<SocketRole> rolesAfter = after.Roles;
                List<SocketRole> rolesAdded = [];
                // Changed role? We can track only additions. Removals are not really sent

                foreach (SocketRole r1 in rolesAfter)
                {
                    bool addedRole = true;
                    foreach (SocketRole r2 in rolesBefore)
                    {
                        if (r1.Id == r2.Id)
                        {
                            addedRole = false;
                            break;
                        }
                    }
                    if (addedRole) rolesAdded.Add(r1);
                }

                if (rolesBefore.Count > 0 && rolesAdded.Count > 0)
                {
                    var msgC = "User " + after.Mention + " has the new role" + (rolesAdded.Count > 1 ? "s:" : ":");
                    var msgL = "User \"" + after.DisplayName + "\" has the new role" + (rolesAdded.Count > 1 ? "s:" : ":");
                    foreach (SocketRole r in rolesAdded)
                    {
                        msgC += r.Mention;
                        msgL += r.Name;
                    }
                    await trackChannel.channel.SendMessageAsync(msgC);
                    Utils.Log(msgL, guild.Name);
                }
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                Utils.Log("Error in DiscordMemberUpdated: " + ex.Message, guild.Name);
            }

            await Task.Delay(10);
        }


        private static async Task SomethingAsync(SocketTextChannel trackChannel, ulong id, string name, string mention, int numMembers)
        {
            await Task.Delay(25000);
            if (tracking.ContainsKey(id))
            {
                string msgC = Utils.GetEmojiSnowflakeID(EmojiEnum.OK) + "  User " + mention + " joined on " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + " (" + numMembers + " members total)";
                string msgL = "+ User " + name + " joined on " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss") + " (" + numMembers + " members total)";
                try
                {
                    await trackChannel.SendMessageAsync(msgC);
                }
                catch (Exception e)
                {
                    Utils.Log("Cannot post in tracking channel: " + e.Message, trackChannel.Guild.Name);
                }
                Utils.Log(msgL, trackChannel.Guild.Name);
                tracking.Remove(id);
            }
        }
    }
}
