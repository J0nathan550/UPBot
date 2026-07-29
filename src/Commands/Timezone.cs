using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeZoneConverter;
using UPBot.UPBot_Code;

/// <summary>
/// This command implements a table for the timezones of the users
/// Users can define their own timezone and can see the local time of other users that defined the timezone
/// author: CPU
/// </summary>

namespace UPBot
{

    [Group("tz", "Commands to check timezones")]
    public class SlashTimezone : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("whattimeis", "Checks the current local time in a timezone")]
        public async Task TZTimeCommand([Summary("timezone", "Timezone to check the local time")] string tz)
        {
            Utils.LogUserCommand(Context);

            try
            {
                List<RankedTimezone> res = GetTimezone(tz, out TimeZoneInfo tzi);
                if (res == null && tzi == null)
                {
                    await RespondAsync($"I cannot find a timezone similar to {tz}.", ephemeral: true);
                }
                else if (tzi != null)
                {
                    DateTime dest = TimeZoneInfo.ConvertTime(DateTime.Now, tzi);
                    await RespondAsync($"Current time in the timezone is {dest:HH:mm:ss}\n{GetTZName(tzi)}");
                }
                else
                {
                    string msg = $"Cannot find a timezone for {tz}, best opportunities are:\n";
                    foreach (var r in res)
                    {
                        if (TZConvert.TryGetTimeZoneInfo(r.IanaName, out TimeZoneInfo ttz))
                        {
                            msg += "(" + r.Score + ") " + GetTZName(ttz) + "\n";
                        }
                    }
                    await RespondAsync(msg, ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "WhatTimeIs", ex), ephemeral: true);
            }
        }

        [SlashCommand("whattimeisfor", "Checks the current local time for an user")]
        public async Task TZTimeGetCommand([Summary("user", "The user for the time")] IUser user)
        {
            Utils.LogUserCommand(Context);

            try
            {
                SocketGuildUser member = Context.Guild.GetUser(user.Id);
                Timezone utz = Database.GetByKey<Timezone>(user.Id);
                if (utz == null)
                {
                    await RespondAsync($"Timezone for user {member.DisplayName} is not defined", ephemeral: true);
                    return;
                }

                if (!TZConvert.TryGetTimeZoneInfo(utz.TimeZoneName, out var tzinfo))
                {
                    await RespondAsync($"Timezone for user {member.DisplayName} is not clear: {utz.TimeZoneName}", ephemeral: true);
                    return;
                }

                DateTime dest = TimeZoneInfo.ConvertTime(DateTime.Now, tzinfo);
                await RespondAsync($"Current time for user {member.DisplayName} is {dest:HH:mm:ss}\n{GetTZName(tzinfo)}");
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "WhatTimeIsFor", ex), ephemeral: true);
            }
        }

        [SlashCommand("set", "Set the timezone for a user")]
        public async Task TZSetCommand([Summary("user", "The user to set the timezone")] IUser user, [Summary("timezone", "Timezone to check the local time")] string tz)
        {
            try
            {
                SocketGuildUser member = Context.Guild.GetUser(user.Id);
                if (!Configs.HasAdminRole(Context.Guild.Id, member.Roles, false) && user.Id != Context.User.Id) { Utils.DefaultNotAllowed(Context); return; }
                Utils.LogUserCommand(Context);

                List<RankedTimezone> res = GetTimezone(tz, out TimeZoneInfo tzi);
                if (res == null && tzi == null)
                {
                    await RespondAsync($"I cannot find a timezone similar to {tz}.", ephemeral: true);
                }
                else if (tzi != null)
                {
                    DateTime dest = TimeZoneInfo.ConvertTime(DateTime.Now, tzi);
                    string tzid = tzi.Id;
                    if (TZConvert.TryWindowsToIana(tzid, out string tzname)) tzid = tzname;
                    Database.Add(new Timezone(user.Id, tzid));
                    await RespondAsync($"Timezone for user {member.DisplayName} is set to {GetTZName(tzi)}. Current time for they is {dest:HH:mm:ss}");
                }
                else
                {
                    string msg = $"Cannot find a timezone for {tz}, best opportunities are:\n";
                    foreach (var r in res)
                    {
                        if (TZConvert.TryGetTimeZoneInfo(r.IanaName, out TimeZoneInfo ttz))
                        {
                            msg += "(" + r.Score + ")  " + ttz.StandardName + " (" + r.IanaName + ") UTC";
                            if (ttz.BaseUtcOffset >= TimeSpan.Zero) msg += "+"; else msg += "-";
                            msg += Math.Abs(ttz.BaseUtcOffset.Hours).ToString("00") + ":" + Math.Abs(ttz.BaseUtcOffset.Minutes).ToString("00");
                            msg += "\n";
                        }
                    }

                    await RespondAsync(msg);
                }
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "Set Timezone", ex), ephemeral: true);
            }
        }


        [SlashCommand("list", "List all timezones with how many users are in each one")]
        public async Task TZListCommand()
        {
            try
            {
                string res = "```\n";
                var list = Database.GetAll<Timezone>();
                Dictionary<string, int> count = [];
                foreach (Timezone t in list)
                {
                    count[t.TimeZoneName] = !count.TryGetValue(t.TimeZoneName, out int value) ? 1 : ++value;
                }
                string bads = "";
                int numbads = 0;
                foreach (var tzcode in count.Keys)
                {
                    if (TZConvert.TryGetTimeZoneInfo(tzcode, out var tzinfo))
                    {
                        string tzid = tzinfo.Id;
                        if (TZConvert.TryWindowsToIana(tzid, out string tzidname)) tzid = tzidname;
                        string tzname = tzid + " (" + tzinfo.DisplayName + ") UTC";
                        if (tzinfo.BaseUtcOffset >= TimeSpan.Zero) tzname += "+"; else tzname += "-";
                        tzname += Math.Abs(tzinfo.BaseUtcOffset.Hours).ToString("00") + ":" + Math.Abs(tzinfo.BaseUtcOffset.Minutes).ToString("00");
                        res += count[tzcode] + (count[tzcode] == 1 ? " user   " : " users  ") + tzname + "\n";
                    }
                    else
                    {
                        bads += tzcode + ", ";
                        numbads++;
                    }
                }
                if (numbads > 0)
                {
                    res += numbads + " Unknown timezones: " + bads[..^2] + "\n";
                }
                res += "```";
                await RespondAsync(res);
            }
            catch (Exception ex)
            {
                if (ex is Discord.Net.HttpException httpEx && httpEx.HttpCode == System.Net.HttpStatusCode.NotFound) return; // Timed out
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "List Timezones", ex), ephemeral: true);
            }
        }



        private class RankedTimezone { public string IanaName; public int Score; }
        private static Dictionary<string, string> fullTZList;


        private static string GetTZName(TimeZoneInfo tzinfo)
        {
            string tzid = tzinfo.Id;
            if (TZConvert.TryWindowsToIana(tzinfo.Id, out string tzname)) tzid = tzname;
            string tzn = tzinfo.StandardName + " / " + tzid + " (UTC";
            if (tzinfo.BaseUtcOffset >= TimeSpan.Zero) tzn += "+"; else tzn += "-";
            tzn += Math.Abs(tzinfo.BaseUtcOffset.Hours).ToString("00") + ":" + Math.Abs(tzinfo.BaseUtcOffset.Minutes).ToString("00") + ")";
            return tzn;
        }

        private int GetTZScore(string src, string dst)
        {
            if (src.Equals(dst, StringComparison.InvariantCultureIgnoreCase)) return 100;
            if (dst.Contains(src, StringComparison.InvariantCultureIgnoreCase)) return (int)(100f * src.Length / dst.Length);

            var srcpts = src.ToLowerInvariant().Split(' ', '/', '(', ')', '_');
            var dstpts = dst.ToLowerInvariant().Split(' ', '/', '(', ')', '_');
            int score = 0;
            foreach (var d in dstpts)
            {
                if (string.IsNullOrWhiteSpace(d)) continue;
                foreach (var s in srcpts)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    if (d.Equals(s)) score += 12;
                    else if (s.Equals("est") || s.Equals("east") || s.Equals("eastern"))
                    {
                        if (d.Equals("e.")) score += 8;
                    }
                    else if (s.Equals("west") || s.Equals("western"))
                    {
                        if (d.Equals("w.")) score += 8;
                    }
                    else if (s.Equals("america") || s.Equals("usa"))
                    {
                        if (d.Equals("us")) score += 6;
                    }
                    else
                    {
                        int min = s.Length;
                        if (d.Length < min) min = d.Length;
                        int dist = StringDistance.DLDistance(s, d);
                        if (dist < min / 5) score += min - dist;
                    }
                }
            }
            return (int)(score * 1f / srcpts.Length);
        }

        private List<RankedTimezone> CheckProx(string inp)
        {
            List<RankedTimezone> res = [];
            foreach (string key in fullTZList.Keys)
            {
                int score = GetTZScore(inp, key);
                if (score > 8)
                {
                    res.Add(new RankedTimezone { IanaName = key, Score = score });
                }
                else
                {
                    score = GetTZScore(inp, fullTZList[key]);
                    if (score >= 8)
                    {
                        res.Add(new RankedTimezone { IanaName = key, Score = score });
                    }
                }
            }
            res.Sort((a, b) => b.Score.CompareTo(a.Score));

            if (res.Count == 0) return null;

            int val = res[0].Score;
            for (int i = res.Count - 1; i > 1; i--)
            {
                if (res[i].Score <= val - 5) res.RemoveAt(i);
            }
            return res;
        }


        private static void InitTimeZones()
        {
            fullTZList = [];
            var work = new Dictionary<string, string>();
            foreach (var t in TZConvert.KnownIanaTimeZoneNames)
                work[t] = t;
            foreach (var t in TZConvert.KnownWindowsTimeZoneIds)
            {
                string key = t;
                if (TZConvert.TryWindowsToIana(t, out string tzidname)) key = tzidname;
                if (!work.ContainsKey(key)) work[key] = t;
                else work[key] += " " + t;
            }
            foreach (var t in TZConvert.KnownRailsTimeZoneNames)
            {
                string key = TZConvert.RailsToIana(t);
                if (!work.ContainsKey(key)) work[key] = t;
                else work[key] += " " + t;
            }

            foreach (string key in work.Keys)
            {
                string val = work[key];
                string cleaned = "";
                string[] parts = val.Split(' ');
                foreach (var part in parts)
                    if (!cleaned.Contains(part, StringComparison.InvariantCultureIgnoreCase)) cleaned += part + " ";
                fullTZList[key] = cleaned.Trim();
            }
        }

        private List<RankedTimezone> GetTimezone(string input, out TimeZoneInfo res)
        {
            if (fullTZList == null) InitTimeZones();
            if (TZConvert.TryGetTimeZoneInfo(input, out TimeZoneInfo tz))
            {
                res = tz;
                return null;
            }
            res = null;
            var list = CheckProx(input);
            if (list?.Count == 1)
            {
                if (TZConvert.TryGetTimeZoneInfo(list[0].IanaName, out tz))
                {
                    res = tz;
                    return null;
                }
            }
            return list;
        }

    }
}