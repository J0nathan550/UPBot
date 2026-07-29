using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

/// <summary>
/// This command implements a WhoIs command.
/// It gives info about a Discord User or yourself
/// author: CPU
/// </summary>
/// 

public class SlashWhoIs : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("whois", "Get information about a specific user (or yourself)")]
    public async Task WhoIsCommand([Summary("user", "The user to get info from")] IUser user = null)
    {
        Utils.LogUserCommand(Context);

        try
        {
            SocketGuildUser invoker = Context.User as SocketGuildUser;
            SocketGuildUser m;

            m = user == null ? invoker : Context.Guild.GetUser(user.Id); // If we do not have a user we use the member that invoked the command
            bool you = m.Id == invoker.Id;

            DateTimeOffset jdate = (m.JoinedAt ?? DateTimeOffset.UtcNow).UtcDateTime;
            string joined = jdate.Year + "/" + jdate.Month + "/" + jdate.Day;
            DateTimeOffset cdate = m.CreatedAt.UtcDateTime;
            string creation = cdate.Year + "/" + cdate.Month + "/" + cdate.Day;

            int daysJ = (int)(DateTime.Now - (m.JoinedAt ?? DateTimeOffset.UtcNow).DateTime).TotalDays;
            int daysA = (int)(DateTime.Now - m.CreatedAt.DateTime).TotalDays;
            double years = daysA / 365.25;

            // Discord.Net doesn't expose a ready-made "member color"; take the highest
            // positioned role that actually has a non-default color, like Discord itself does.
            Color memberColor = m.Roles
                .Where(r => r.Color.RawValue != 0)
                .OrderByDescending(r => r.Position)
                .Select(r => r.Color)
                .DefaultIfEmpty(Color.Default)
                .First();

            string title = "Who is the user " + m.DisplayName + "#" + m.Discriminator;
            string description = m.Username + " joined on " + joined + " (" + daysJ + " days)\n Account created on " +
                                 creation + " (" + daysA + " days, " + years.ToString("N1") + " years)";
            var embed = Utils.BuildEmbed(title, description, memberColor);
            embed.WithThumbnailUrl(m.GetDisplayAvatarUrl() ?? m.GetDefaultAvatarUrl());

            embed.AddField("Is you", you ? "✓" : "❌", true);
            embed.AddField("Is a bot", m.IsBot ? "🤖" : "❌", true);
            embed.AddField("Is the boss", m.Id == Context.Guild.OwnerId ? "👑" : "❌", true);
            embed.AddField("Is Muted", m.IsMuted ? "✓" : "❌", true);
            embed.AddField("Is Deafened", m.IsDeafened ? "✓" : "❌", true);

            if (m.Nickname != null) embed.AddField("Is called", m.Nickname, true);
            embed.AddField("Avatar Hex Color", memberColor.ToString(), true);

            if (m.PremiumSince != null)
            {
                DateTimeOffset bdate = ((DateTimeOffset)m.PremiumSince).UtcDateTime;
                string booster = bdate.Year + "/" + bdate.Month + "/" + bdate.Day;
                embed.AddField("Booster", "From " + booster, true);
            }
            if (m.PublicFlags != null) embed.AddField("Flags", m.PublicFlags.ToString(), true); // Only the default flags will be shown. This bot will not be very diffused so probably we do not need specific checks for flags

            string roles = "";
            int num = 0;
            foreach (SocketRole role in m.Roles)
            {
                if (role.IsEveryone) continue;
                roles += role.Mention + " ";
                num++;
            }
            if (num == 1)
                embed.AddField("Role", roles);
            else if (num != 0)
                embed.AddField(num + " Roles", roles);

            string perms = ""; // Not all permissions are shown
            GuildPermissions gp = m.GuildPermissions;
            if (gp.Has(GuildPermission.CreateInstantInvite)) perms += ", Invite";
            if (gp.Has(GuildPermission.KickMembers)) perms += ", Kick";
            if (gp.Has(GuildPermission.BanMembers)) perms += ", Ban";
            if (gp.Has(GuildPermission.Administrator)) perms += ", Admin";
            if (gp.Has(GuildPermission.ManageChannels)) perms += ", Manage Channels";
            if (gp.Has(GuildPermission.ManageGuild)) perms += ", Manage Server";
            if (gp.Has(GuildPermission.AddReactions)) perms += ", Reactions";
            if (gp.Has(GuildPermission.ViewAuditLog)) perms += ", Audit";
            if (gp.Has(GuildPermission.ManageMessages)) perms += ", Manage Messages";
            if (gp.Has(GuildPermission.EmbedLinks)) perms += ", Links";
            if (gp.Has(GuildPermission.AttachFiles)) perms += ", Files";
            if (gp.Has(GuildPermission.UseExternalEmojis)) perms += ", Ext Emojis";
            if (gp.Has(GuildPermission.Speak)) perms += ", Speak";
            if (gp.Has(GuildPermission.ManageRoles)) perms += ", Manage Roles";
            if (gp.Has(GuildPermission.ManageEmojisAndStickers)) perms += ", Manage Emojis";
            if (gp.Has(GuildPermission.UseApplicationCommands)) perms += ", Use Bot";
            if (gp.Has(GuildPermission.CreatePublicThreads)) perms += ", Use Threads";
            if (perms.Length > 0) embed.AddField("Permissions", perms[2..]);

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "WhoIs", ex));
        }
    }
}
