using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

/// <summary>
/// Provide some server stats
/// author: CPU
/// </summary>
public class SlashStats : InteractionModuleBase<SocketInteractionContext>
{

    /*
    Stats
    > Show global stats: 
        - global server stats (times and numbers)
        - number of roles with numebr of people for each role
        - interaction to check most mentioned people in current channel (or type a channel)
        - interaction to most used emojis (or type a channel)
        - interaction to most posting and mentioned roles (or type a channel)
        - button for all 3 stats together

    stats roles #channel
    stats mentions #channel
    stats emojis #channel
    stats all #channel  
    */

    public enum StatsTypes
    {
        [ChoiceDisplay("Only server")] OnlyServer,
        [ChoiceDisplay("Roles")] Roles,
        [ChoiceDisplay("Mentions")] Mentions,
        [ChoiceDisplay("Emojis")] Emojis,
        [ChoiceDisplay("All stats")] AllStats
    }


    [SlashCommand("stats", "Provides server stats, including detailed stats for roles, mentions, and emojis when specified")]
    public async Task StatsCommand([Summary("what", "What type of stats to show")] StatsTypes? what)
    {
        Utils.LogUserCommand(Context);

        try
        {
            if (what == null || what == StatsTypes.OnlyServer)
            {
                await RespondAsync(embed: GenerateStatsEmbed().Build());

            }
            else if (what == StatsTypes.AllStats)
            {
                await RespondAsync(embed: GenerateStatsEmbed().Build());

                var fup = await FollowupAsync("Calculating emojis stats...");
                await FollowupAsync(await CalculateEmojis());
                await fup.DeleteAsync();

                fup = await FollowupAsync("Calculating mentions stats...");
                await FollowupAsync(await CalculateUserMentions());
                await fup.DeleteAsync();

                fup = await FollowupAsync("Calculating roles stats...");
                await FollowupAsync(await CalculateRoleMentions());
                await fup.DeleteAsync();

            }
            else if (what == StatsTypes.Emojis)
            {
                await RespondAsync(embed: GenerateStatsEmbed().Build());
                var fup = await FollowupAsync("Calculating emojis stats...");
                await fup.DeleteAsync();
                await FollowupAsync(await CalculateEmojis());

            }
            else if (what == StatsTypes.Mentions)
            {
                await RespondAsync(embed: GenerateStatsEmbed().Build());
                var fup = await FollowupAsync("Calculating mentions stats...");
                await fup.DeleteAsync();
                await FollowupAsync(await CalculateUserMentions());

            }
            else if (what == StatsTypes.Roles)
            {
                await RespondAsync(embed: GenerateStatsEmbed().Build());
                var fup = await FollowupAsync("Calculating roles stats...");
                await fup.DeleteAsync();
                await FollowupAsync(await CalculateRoleMentions());
            }

        }
        catch (Exception ex)
        {
            await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "Stats", ex));
        }
    }


    private EmbedBuilder GenerateStatsEmbed()
    {
        EmbedBuilder e = new();
        SocketGuild g = Context.Guild;

        e.Description = " ----  ---- Stats ----  ---- \n_" + g.Description + "_";

        // Discord.Net's SocketGuild doesn't surface the REST "approximate member/presence
        // count" fields DSharpPlus exposed (those come from the with_counts guild preview,
        // not the gateway cache), so we fall back to the live gateway member count only.
        int m2 = g.MemberCount;
        int? m3 = g.MaxMembers;
        string members = m2 + (m3 != null ? "/" + m3 + "max" : "");
        bool isLarge = g.MemberCount >= 250; // Discord's own "large guild" threshold
        e.AddField("Members", members + (isLarge ? " (large)" : ""), true);
        int? s1 = g.PremiumSubscriptionCount;
        if (s1 != null) e.AddField("Boosters", s1.ToString(), true);

        double days = (DateTime.Now - g.CreatedAt.UtcDateTime).TotalDays;
        e.AddField("Server created", (int)days + " days ago", true);
        double dailyms = m2 / days;
        e.AddField("Daily members", dailyms.ToString("N1") + " members per day", true);

        e.WithTitle("Stats for " + g.Name);
        e.WithThumbnailUrl(g.IconUrl);
        e.WithImageUrl(g.BannerUrl);

        int numtc = 0, numvc = 0, numnc = 0;
        foreach (var c in g.Channels)
        {
            if (c is SocketVoiceChannel) numvc++;
            else if (c is SocketTextChannel tc && tc.IsNsfw) numnc++;
            else numtc++;
        }

        if (g.NsfwLevel == NsfwLevel.Explicit || g.NsfwLevel == NsfwLevel.AgeRestricted)
            e.AddField("NSFW", "NSFW server\nFilter level: " + g.ExplicitContentFilter.ToString() + "\nNSFW restriction type: " + g.NsfwLevel.ToString(), true);

        e.AddField("Roles:", g.Roles.Count + " roles", true);

        e.AddField("Cannels", numtc + " text, " + numvc + " voice" + (numnc > 0 ? ", " + numnc + " nsfw" : "") +
          (g.SystemChannel == null ? "" : "\nSystem channel: " + g.SystemChannel.Mention) +
          (g.RulesChannel == null ? "" : "\nRules channel: " + g.RulesChannel.Mention), false);

        string emojis;
        if (g.Emotes.Count > 0)
        {
            emojis = g.Emotes.Count + " custom emojis: ";
            foreach (var emj in g.Emotes) emojis += Utils.GetEmojiSnowflakeID(emj) + " ";
            e.AddField("Emojis:", emojis, true);
        }
        return e;
    }

    private async Task<string> CalculateEmojis()
    {
        Dictionary<string, int> count = [];

        ITextChannel channel = Context.Channel as ITextChannel;
        var msgs = await channel.GetMessagesAsync(1000).FlattenAsync();
        foreach (var m in msgs)
        {
            var emjs = m.Reactions;
            foreach (var r in emjs)
            {
                string snowflake = Utils.GetEmojiSnowflakeID(r.Key);
                if (snowflake == null) continue;
                if (count.ContainsKey(snowflake)) count[snowflake] += r.Value.ReactionCount;
                else count[snowflake] = r.Value.ReactionCount;
            }
        }
        List<KeyValuePair<string, int>> list = [];
        foreach (var k in count.Keys) list.Add(new KeyValuePair<string, int>(k, count[k]));
        list.Sort((a, b) => { return b.Value.CompareTo(a.Value); });

        string res = "\n_Used emojis_: used " + list.Count + " different emojis(as reactions):\n  ";
        for (int i = 0; i < 25 && i < list.Count; i++)
        {
            res += $"**{list[i].Key}**({list[i].Value}) ";
        }
        if (list.Count >= 25) res += " _showing only the first, most used, 25._";
        return res;
    }

    private async Task<string> CalculateUserMentions()
    {
        Dictionary<string, int> count = [];
        Dictionary<ulong, int> askers = [];
        ITextChannel channel = Context.Channel as ITextChannel;
        var msgs = await channel.GetMessagesAsync(1000).FlattenAsync();

        foreach (var m in msgs)
        {
            var mens = m.MentionedUserIds;
            foreach (var rid in mens)
            {
                SocketGuildUser ru = Context.Guild.GetUser(rid);
                string snowflake = ru?.Username;
                if (snowflake == null) continue;
                snowflake = snowflake.Replace("_", "\\_");
                count[snowflake] = count.TryGetValue(snowflake, out int currentCount) ? currentCount + 1 : 1;
                askers[m.Author.Id] = askers.TryGetValue(m.Author.Id, out int currentAskerCount) ? currentAskerCount + 1 : 1;
            }
        }

        // Sort mentioned users by count
        var sortedMentions = count.OrderByDescending(x => x.Value).ToList();

        // Get users who mentioned others
        var sortedAskers = new List<KeyValuePair<string, int>>();
        foreach (var askerId in askers.Keys)
        {
            try
            {
                SocketGuildUser member = Context.Guild.GetUser(askerId);
                if (member != null)
                {
                    sortedAskers.Add(new KeyValuePair<string, int>(member.Username, askers[askerId]));
                }
            }
            catch
            {
                // Member might have left the server, skip
                continue;
            }
        }
        sortedAskers = [.. sortedAskers.OrderByDescending(x => x.Value)];

        // Build result string
        string res = $"\n_Mentioned users_: {sortedMentions.Count} users have been mentioned:\n  ";
        for (int i = 0; i < Math.Min(25, sortedMentions.Count); i++)
        {
            res += $"**{sortedMentions[i].Key}**({sortedMentions[i].Value}) ";
        }
        if (sortedMentions.Count > 25) res += " *showing only the first, most mentioned, 25.*";

        res += $"\n_Users mentioning_: {sortedAskers.Count} users have mentioned other users:\n  ";
        for (int i = 0; i < Math.Min(25, sortedAskers.Count); i++)
        {
            res += $"**{sortedAskers[i].Key}**({sortedAskers[i].Value}) ";
        }
        if (sortedAskers.Count > 25) res += " *showing only the first, most mentioned, 25.*";

        return res;
    }

    private async Task<string> CalculateRoleMentions()
    {
        Dictionary<string, int> count = [];
        Dictionary<ulong, int> askers = [];
        ITextChannel channel = Context.Channel as ITextChannel;
        var msgs = await channel.GetMessagesAsync(1000).FlattenAsync();

        foreach (var m in msgs)
        {
            var mens = m.MentionedRoleIds;
            foreach (var rid in mens)
            {
                SocketRole role = Context.Guild.GetRole(rid);
                string snowflake = role?.Name;
                if (snowflake == null) continue;
                snowflake = snowflake.Replace("_", "\\_");
                count[snowflake] = count.TryGetValue(snowflake, out int currentCount) ? currentCount + 1 : 1;
                askers[m.Author.Id] = askers.TryGetValue(m.Author.Id, out int currentAskerCount) ? currentAskerCount + 1 : 1;
            }
        }

        // Sort mentioned roles by count
        var sortedMentions = count.OrderByDescending(x => x.Value).ToList();

        // Get users who mentioned roles
        var sortedAskers = new List<KeyValuePair<string, int>>();
        foreach (var askerId in askers.Keys)
        {
            try
            {
                SocketGuildUser member = Context.Guild.GetUser(askerId);
                if (member != null)
                {
                    sortedAskers.Add(new KeyValuePair<string, int>(member.Username, askers[askerId]));
                }
            }
            catch
            {
                // Member might have left the server, skip
                continue;
            }
        }
        sortedAskers = [.. sortedAskers.OrderByDescending(x => x.Value)];

        // Build result string
        string res = $"\n_Mentioned roles_: {sortedMentions.Count} roles have been mentioned:\n  ";
        for (int i = 0; i < Math.Min(25, sortedMentions.Count); i++)
        {
            res += $"**{sortedMentions[i].Key}**({sortedMentions[i].Value}) ";
        }
        if (sortedMentions.Count > 25) res += " *showing only the first, most mentioned, 25.*";

        res += $"\n_Users mentioning_: {sortedAskers.Count} users have mentioned the roles:\n  ";
        for (int i = 0; i < Math.Min(25, sortedAskers.Count); i++)
        {
            res += $"**{sortedAskers[i].Key}**({sortedAskers[i].Value}) ";
        }
        if (sortedAskers.Count > 25) res += " *showing only the first, most mentioned, 25.*";

        return res;
    }

}
