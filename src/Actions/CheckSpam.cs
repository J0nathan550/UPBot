using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

/// <summary>
/// Scam/spam moderation, 1:1 ported from the CoopAndreas bot's SpamModerator.
///
/// Differences from a naive port, required because UPBot (unlike the CoopAndreas bot)
/// runs on many guilds at once:
///  - Every tracker is keyed by (GuildId, UserId), not just UserId, so activity in one
///    server can never be confused with, or count towards, activity in another.
///  - The "log channel" is each guild's own configured TrackChannel (set via /setup),
///    instead of a single hardcoded channel id.
///  - Enable/disable, and the blacklist/whitelist link lists, come from this guild's
///    SpamProtection / SpamLink rows (managed via /setup) instead of being global.
///
/// Everything else (thresholds, scoring, ordering of side effects, DM/log content)
/// mirrors the original bot as closely as DSharpPlus allows.
/// </summary>
namespace UPBot
{
    public class CheckSpam
    {
        private static readonly Regex linkRE = new(@"http[s]?://([^/]+)/", RegexOptions.Compiled);

        private static readonly Dictionary<(ulong Guild, ulong User), List<MessageRecord>> spamTracker = [];
        private static readonly Dictionary<(ulong Guild, ulong User), UserActivity> userActivity = [];

        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        public static DiscordUser SpamCheckTimeout;

        private const int TIMEOUT_THRESHOLD = 3;
        private const int SAME_CHANNEL_SPAM_THRESHOLD = 3;
        private const int TIME_WINDOW_MINUTES = 5;
        private const int TIMEOUT_DAYS = 7;
        private const int SHORT_TIMEOUT_MINUTES = 10;

        private const string TIMEOUT_REASON =
            "Spammer/Scammer - Posting same content across multiple channels. If you think it is a mistake, contact admins.";
        private const string SAME_CHANNEL_TIMEOUT_REASON =
            "Spamming the same message repeatedly in a channel.";
        private const string IMAGE_SPAM_TIMEOUT_REASON =
            "Suspected scam/spam - Single message with multiple images and minimal text. If you think this is a mistake, please contact the server admins.";
        private const string IMAGE_SPAM_WARN_REASON =
            "Your message was removed because it looked like spam (multiple images with very little text). If this was a mistake, please contact the server admins.";

        private const int SUSPICIOUS_IMAGE_COUNT = 3;
        private const int MINIMAL_TEXT_LENGTH = 20;
        private const int ESTABLISHED_MEMBER_THRESHOLD = 5;
        private const int NEW_ACCOUNT_WINDOW_DAYS = 30;
        private const int SPAM_SCORE_TIMEOUT_THRESHOLD = 4;
        private const int SPAM_SCORE_WARN_THRESHOLD = 2;

        private static readonly HashSet<string> SuspiciousShortPhrases = new(StringComparer.OrdinalIgnoreCase)
        {
            "bro", "hey", "check", "check this", "look", "wow", "omg", "!!!",
            "free", "win", "claim", "bonus", "giveaway", "profit", "crypto",
            "🔥", "👀", "💰", "🎁", "🚀"
        };

        private sealed class UserActivity
        {
            public int LegitimateMessageCount { get; set; }
            public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
        }

        private sealed class MessageRecord
        {
            public string Content { get; set; } = string.Empty;
            public int ImageCount { get; set; }
            public ulong ChannelId { get; set; }
            public ulong MessageId { get; set; }
            public DateTime Timestamp { get; set; }
            public List<string> ImageUrls { get; set; } = [];
        }

        internal static async Task CheckMessageUpdate(DiscordClient _, MessageUpdateEventArgs args)
        {
            await CheckMessage(args.Guild, args.Author, args.Message);
        }

        internal static async Task CheckMessageCreate(DiscordClient _, MessageCreateEventArgs args)
        {
            await CheckMessage(args.Guild, args.Author, args.Message);
        }

        private static async Task CheckMessage(DiscordGuild guild, DiscordUser author, DiscordMessage message)
        {
            if (guild == null || author == null || message == null || author.Id == Configs.BotId)
                return;

            if (SpamCheckTimeout != null && SpamCheckTimeout.Id == author.Id)
            {
                SpamCheckTimeout = null;
                Utils.Log("Probably self post of spam ignored.", guild.Name);
                return;
            }

            DiscordMember authorMember;
            try
            {
                authorMember = await guild.GetMemberAsync(author.Id);
            }
            catch (Exception ex)
            {
                Utils.Log("Unable to resolve guild member for spam check: " + ex.Message, guild.Name);
                return;
            }

            if (authorMember == null || authorMember.IsBot)
                return;

            try
            {
                SpamProtection sp;
                if (!Configs.SpamProtections.TryGetValue(guild.Id, out var configuredProtection) || configuredProtection == null)
                {
                    sp = new SpamProtection(guild.Id)
                    {
                        protectDiscord = true,
                        protectSteam = false,
                        protectEpic = false
                    };
                    Configs.SpamProtections[guild.Id] = sp;
                }
                else
                {
                    sp = configuredProtection;
                }

                // protectSteam / protectEpic are reserved for future link-category-specific
                // filtering; today, any protection flag being on enables the full engine below.
                if (!sp.protectDiscord && !sp.protectSteam && !sp.protectEpic)
                    return;

                string content = message.Content ?? string.Empty;
                int imageCount = GetImageCount(message);
                List<string> imageUrls = GetImageUrls(message);

                if (string.IsNullOrWhiteSpace(content) && imageCount == 0)
                    return;

                if (await CheckCustomLinkModeration(guild, authorMember, message, content))
                    return;

                var key = (guild.Id, authorMember.Id);

                bool passedImageSpamCheck = await CheckSingleMessageImageSpam(guild, authorMember, message, content, imageCount, imageUrls, key);
                if (!passedImageSpamCheck)
                    return;

                TrackLegitimateMessage(key, DateTime.UtcNow);

                CleanOldRecords(key, DateTime.UtcNow);
                if (!spamTracker.TryGetValue(key, out List<MessageRecord> value))
                {
                    value = [];
                    spamTracker[key] = value;
                }

                value.Add(new MessageRecord
                {
                    Content = content,
                    ImageCount = imageCount,
                    ChannelId = message.Channel.Id,
                    MessageId = message.Id,
                    Timestamp = DateTime.UtcNow,
                    ImageUrls = imageUrls
                });

                var matchingRecords = value
                    .Where(r => r.Content == content && r.ImageCount == imageCount)
                    .ToList();

                var sameChannelMatches = matchingRecords.Where(r => r.ChannelId == message.Channel.Id).ToList();
                if (sameChannelMatches.Count >= SAME_CHANNEL_SPAM_THRESHOLD)
                {
                    var allImageUrls = matchingRecords.SelectMany(r => r.ImageUrls).Distinct().ToList();
                    await TimeoutAndDelete(
                        guild, authorMember, message,
                        "Same Channel Spam", SAME_CHANNEL_TIMEOUT_REASON,
                        TimeSpan.FromMinutes(SHORT_TIMEOUT_MINUTES),
                        deleteThisMessage: false, allImageUrls);
                    spamTracker.Remove(key);
                    return;
                }

                bool alreadyInThisChannel = matchingRecords.Any(r =>
                    r.ChannelId == message.Channel.Id &&
                    r.MessageId != message.Id &&
                    (DateTime.UtcNow - r.Timestamp).TotalSeconds < 3);
                if (alreadyInThisChannel)
                {
                    value.RemoveAll(r => r.MessageId == message.Id);
                    return;
                }

                var uniqueChannels = matchingRecords.Select(r => r.ChannelId).Distinct().ToList();
                int channelCount = uniqueChannels.Count;

                if (channelCount >= 2)
                    Utils.Log($"[SPAM TRACKER] User {authorMember.Username} posted same content in {channelCount} channel(s)", guild.Name);

                if (channelCount >= TIMEOUT_THRESHOLD)
                {
                    var allImageUrls = matchingRecords.SelectMany(r => r.ImageUrls).Distinct().ToList();
                    await TimeoutAndDelete(
                        guild, authorMember, message,
                        "Cross-Channel Spam", TIMEOUT_REASON,
                        TimeSpan.FromDays(TIMEOUT_DAYS),
                        deleteThisMessage: true, allImageUrls);
                    spamTracker.Remove(key);
                    return;
                }

                if (channelCount >= 2)
                {
                    try
                    {
                        await message.DeleteAsync("Duplicate content detected");
                    }
                    catch (Exception ex)
                    {
                        Utils.Log("Could not delete duplicate message: " + ex.Message, guild.Name);
                    }
                    return;
                }
            }
            catch (NullReferenceException ex)
            {
                Utils.Log(ex.Message, null);
                Utils.Log(ex.ToString(), null);
            }
            catch (Exception ex)
            {
                if (ex is DSharpPlus.Exceptions.NotFoundException) return;
                await ReportErrorToModChannel(guild, "CheckSpam.CheckMessage", ex);
            }
        }

        private static async Task ReportErrorToModChannel(DiscordGuild guild, string context, Exception ex)
        {
            try
            {
                if (Configs.TrackChannels.TryGetValue(guild.Id, out TrackChannel trackChannel) && trackChannel?.channel != null)
                    await trackChannel.channel.SendMessageAsync(Utils.GenerateErrorAnswer(guild.Name, context, ex));
                else
                    Utils.Log($"[ERROR] {context}: {ex.Message} (no mod/log channel configured to report to)", guild.Name);
            }
            catch (Exception logEx)
            {
                Utils.Log($"[ERROR] Failed to report error to mod channel: {logEx.Message}", guild.Name);
            }
        }

        private static async Task<bool> CheckCustomLinkModeration(DiscordGuild guild, DiscordMember authorMember, DiscordMessage message, string content)
        {
            foreach (Match m in linkRE.Matches(content.ToLowerInvariant()))
            {
                if (!m.Success) continue;
                string link = m.Groups[1].Value;

                foreach (var s in Configs.SpamLinks[guild.Id])
                {
                    if (link.Contains(s, StringComparison.OrdinalIgnoreCase))
                    {
                        await TimeoutAndDelete(
                            guild, authorMember, message,
                            "Custom Blacklisted Link", $"Blocked scam link in {guild.Name}",
                            TimeSpan.FromHours(12),
                            deleteThisMessage: true, imageUrls: []);
                        return true;
                    }
                }

                bool whitelisted = Configs.WhiteListLinks[guild.Id].Any(s => link.Contains(s, StringComparison.OrdinalIgnoreCase));
                if (whitelisted)
                    return false;
            }

            return false;
        }

        private static async Task<bool> CheckSingleMessageImageSpam(
            DiscordGuild guild, DiscordMember authorMember, DiscordMessage message,
            string content, int imageCount, List<string> imageUrls, (ulong Guild, ulong User) key)
        {
            if (imageCount < SUSPICIOUS_IMAGE_COUNT)
                return true;

            userActivity.TryGetValue(key, out UserActivity activity);
            int legitCount = activity?.LegitimateMessageCount ?? 0;
            bool isEstablished = legitCount >= ESTABLISHED_MEMBER_THRESHOLD;
            if (isEstablished)
                return true;

            int score = 2;
            if (imageCount >= 5) score += 1;

            string trimmed = content.Trim();
            bool emptyText = string.IsNullOrWhiteSpace(trimmed);
            bool minimalText = trimmed.Length <= MINIMAL_TEXT_LENGTH;
            if (emptyText)
            {
                score += 1;
            }
            else if (minimalText)
            {
                bool isSuspiciousPhrase = SuspiciousShortPhrases.Any(p =>
                    trimmed.Equals(p, StringComparison.OrdinalIgnoreCase) || trimmed.Contains(p, StringComparison.OrdinalIgnoreCase));
                if (isSuspiciousPhrase) score += 1;
            }

            bool isUnknownAccount = legitCount == 0;
            bool isNewAccount = activity == null || (DateTime.UtcNow - activity.FirstSeen).TotalDays < NEW_ACCOUNT_WINDOW_DAYS;
            if (isUnknownAccount) score += 1;
            if (isNewAccount) score += 1;

            Utils.Log(
                $"[IMAGE SPAM CHECK] User {authorMember.Username} | Score: {score} | Images: {imageCount} | " +
                $"Text: \"{trimmed}\" | LegitMessages: {legitCount}", guild.Name);

            if (score >= SPAM_SCORE_TIMEOUT_THRESHOLD)
            {
                await TimeoutAndDelete(
                    guild, authorMember, message,
                    "Image Spam (Single Message)", IMAGE_SPAM_TIMEOUT_REASON,
                    TimeSpan.FromDays(TIMEOUT_DAYS),
                    deleteThisMessage: true, imageUrls);
                return false;
            }

            if (score >= SPAM_SCORE_WARN_THRESHOLD)
            {
                await WarnAndDelete(
                    guild, authorMember, message,
                    "Image Spam Warning (Deleted, No Timeout)", IMAGE_SPAM_WARN_REASON,
                    content, imageCount, imageUrls);
                return false;
            }

            return true;
        }

        private static void TrackLegitimateMessage((ulong Guild, ulong User) key, DateTime now)
        {
            if (!userActivity.TryGetValue(key, out UserActivity activity))
            {
                activity = new UserActivity { FirstSeen = now };
                userActivity[key] = activity;
            }
            activity.LegitimateMessageCount++;
        }

        private static void CleanOldRecords((ulong Guild, ulong User) key, DateTime currentTime)
        {
            if (!spamTracker.TryGetValue(key, out List<MessageRecord> value))
                return;

            var cutoff = currentTime.AddMinutes(-TIME_WINDOW_MINUTES);
            var filtered = value.Where(r => r.Timestamp > cutoff).ToList();
            if (filtered.Count == 0)
                spamTracker.Remove(key);
            else
                spamTracker[key] = filtered;
        }

        private static int GetImageCount(DiscordMessage message)
        {
            int count = message.Attachments.Count;
            foreach (var embed in message.Embeds)
            {
                if (embed.Image != null && !string.IsNullOrWhiteSpace(embed.Image.ToString())) count++;
                if (embed.Thumbnail != null && !string.IsNullOrWhiteSpace(embed.Thumbnail.ToString())) count++;
            }
            return count;
        }

        private static List<string> GetImageUrls(DiscordMessage message)
        {
            List<string> urls = [];
            foreach (var attachment in message.Attachments)
            {
                if (!string.IsNullOrWhiteSpace(attachment.Url))
                    urls.Add(attachment.Url);
            }

            foreach (var embed in message.Embeds)
            {
                if (embed.Image != null && !string.IsNullOrWhiteSpace(embed.Image.ToString()))
                    urls.Add(embed.Image.ToString());
                if (embed.Thumbnail != null && !string.IsNullOrWhiteSpace(embed.Thumbnail.ToString()))
                    urls.Add(embed.Thumbnail.ToString());
            }

            return urls;
        }

        /// <summary>
        /// Times a user out, DMs them, writes the anti-spam log embed, and sweeps up
        /// their other recent messages guild-wide - mirrors SpamModerator's three
        /// "confirmed spam" branches (same-channel, cross-channel, image-spam-timeout).
        /// Logging always happens before any deletion, since LogAction needs the
        /// original message to still exist to link/forward it.
        /// </summary>
        private static async Task TimeoutAndDelete(
            DiscordGuild guild, DiscordMember authorMember, DiscordMessage message,
            string actionType, string reason, TimeSpan duration,
            bool deleteThisMessage, List<string> imageUrls)
        {
            await LogAction(
                guild, authorMember, actionType, reason, FormatDuration(duration),
                message.Content ?? "", imageUrls?.Count ?? 0, imageUrls, message);

            if (deleteThisMessage)
            {
                try
                {
                    await message.DeleteAsync(reason);
                }
                catch (Exception ex)
                {
                    Utils.Log("Failed to delete suspected scam message: " + ex.Message, guild.Name);
                }
            }

            await TimeoutUser(guild, authorMember, duration, reason);
            await SendDmNotification(authorMember, guild.Name, reason, FormatDuration(duration));
            await DeleteAllRecentMessages(guild, authorMember.Id);

            Utils.Log($"[SPAM] {actionType} action taken against {authorMember.Username}: {reason}", guild.Name);
        }

        /// <summary>
        /// Deletes + logs + DMs, but does NOT time out the user and does NOT sweep other
        /// channels - mirrors SpamModerator's "warning only" image-spam branch.
        /// </summary>
        private static async Task WarnAndDelete(
            DiscordGuild guild, DiscordMember authorMember, DiscordMessage message,
            string actionType, string reason, string content, int imageCount, List<string> imageUrls)
        {
            await LogAction(guild, authorMember, actionType, reason, "None (warning only)", content, imageCount, imageUrls, message);
            await SendDmNotification(authorMember, guild.Name, reason, null);

            try
            {
                await message.DeleteAsync(reason);
            }
            catch (Exception ex)
            {
                Utils.Log("Failed to delete suspicious message: " + ex.Message, guild.Name);
            }

            Utils.Log($"[SPAM] Warned and deleted message from {authorMember.Username}: {reason}", guild.Name);
        }

        private static async Task TimeoutUser(DiscordGuild guild, DiscordMember user, TimeSpan duration, string reason)
        {
            try
            {
                await user.TimeoutAsync(DateTimeOffset.Now + duration, reason);
                Utils.Log($"[TIMEOUT] Timed out {user.Username} ({user.Id}) for {duration.TotalMinutes:0} min — {reason}", guild.Name);
            }
            catch (Exception ex)
            {
                Utils.Log($"Failed to timeout {user.Username}: {ex.Message}", guild.Name);
            }
        }

        private static async Task SendDmNotification(DiscordMember user, string guildName, string reason, string duration)
        {
            try
            {
                var dm = await user.CreateDmChannelAsync();
                if (dm == null) return;

                var eb = new DiscordEmbedBuilder()
                    .WithTitle("⚠️ Action Taken on Your Account")
                    .WithColor(Utils.Red)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .AddField("Server", guildName, true);

                if (!string.IsNullOrWhiteSpace(duration))
                    eb.AddField("Duration", duration, true);

                eb.AddField("Reason", reason, false);
                eb.WithFooter("If you believe this is a mistake, please contact the server admins.");

                await dm.SendMessageAsync(eb.Build());
                Utils.Log($"[DM] Sent notification to {user.Username}", guildName);
            }
            catch (Exception ex)
            {
                Utils.Log($"[DM] Could not DM {user.Username}: {ex.Message}", guildName);
            }
        }

        private static async Task DeleteAllRecentMessages(DiscordGuild guild, ulong userId)
        {
            int deletedCount = 0;
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-TIME_WINDOW_MINUTES);

            foreach (var channel in guild.Channels.Values)
            {
                if (channel.Type != ChannelType.Text)
                    continue;

                try
                {
                    var messages = await channel.GetMessagesAsync(100);
                    foreach (var msg in messages.Where(m => m.Author != null && m.Author.Id == userId && m.Timestamp.UtcDateTime > cutoff))
                    {
                        try { await msg.DeleteAsync(); deletedCount++; }
                        catch (Exception ex) { Utils.Log($"Could not delete {msg.Id}: {ex.Message}", guild.Name); }
                    }
                }
                catch (Exception ex)
                {
                    Utils.Log($"Could not read #{channel.Name}: {ex.Message}", guild.Name);
                }
            }

            Utils.Log($"[CLEANUP] Deleted {deletedCount} messages from user {userId}", guild.Name);
        }

        private static async Task<List<(MemoryStream Stream, string Filename)>> DownloadImagesToMemory(List<string> imageUrls)
        {
            var result = new List<(MemoryStream, string)>();

            foreach (var url in imageUrls)
            {
                try
                {
                    var bytes = await httpClient.GetByteArrayAsync(url);

                    string filename = $"spam_{DateTime.UtcNow.Ticks}_{Path.GetFileName(new Uri(url).LocalPath)}";
                    if (string.IsNullOrWhiteSpace(Path.GetExtension(filename)))
                        filename += ".png";

                    result.Add((new MemoryStream(bytes), filename));
                }
                catch (Exception ex)
                {
                    Utils.Log($"[ERROR] Image download failed for {url}: {ex.Message}", null);
                }
            }

            return result;
        }

        private static async Task LogAction(
            DiscordGuild guild, DiscordMember user, string actionType, string reason, string duration,
            string messageContent, int imageCount, List<string> imageUrls, DiscordMessage originalMessage)
        {
            try
            {
                if (!Configs.TrackChannels.TryGetValue(guild.Id, out TrackChannel trackChannel) || trackChannel?.channel == null)
                {
                    Utils.Log("[SPAM] No tracking channel configured for this server (use /setup); skipping anti-spam log.", guild.Name);
                    return;
                }

                DiscordChannel logChannel = trackChannel.channel;

                var eb = new DiscordEmbedBuilder()
                    .WithTitle("🚨 Anti-Spam Action")
                    .WithColor(Utils.Red)
                    .WithTimestamp(DateTimeOffset.UtcNow)
                    .AddField("User", $"{user.Username} ({user.Mention})", true)
                    .AddField("User ID", user.Id.ToString(), true)
                    .AddField("Action Type", actionType, true)
                    .AddField("Timeout Duration", duration, true)
                    .AddField("Reason", reason, false);

                if (!string.IsNullOrWhiteSpace(messageContent))
                {
                    string truncated = messageContent.Length > 1024
                        ? string.Concat(messageContent.AsSpan(0, 1021), "...")
                        : messageContent;
                    eb.AddField("Message Content", truncated, false);
                }

                if (imageCount > 0)
                    eb.AddField("Images / Attachments", imageCount.ToString(), true);

                DiscordMessageBuilder mb = new DiscordMessageBuilder().AddEmbed(eb.Build());
                List<(MemoryStream Stream, string Filename)> imageData = [];

                if (imageUrls != null && imageUrls.Count > 0)
                {
                    Utils.Log($"[LOG] Downloading {imageUrls.Count} image(s) from {user.Username}'s message to re-attach…", guild.Name);
                    imageData = await DownloadImagesToMemory(imageUrls);

                    if (imageData.Count > 0)
                    {
                        const int maxFiles = 10;
                        if (imageData.Count > maxFiles)
                            eb.AddField("⚠️ Truncated", $"Showing {maxFiles} of {imageData.Count} images.", false);

                        mb = new DiscordMessageBuilder().AddEmbed(eb.Build());
                        foreach (var (stream, filename) in imageData.Take(maxFiles))
                            mb.AddFile(filename, stream);
                    }
                    else
                    {
                        eb.AddField("⚠️ Image Backup", "Images could not be downloaded (URLs may have expired).", false);
                        mb = new DiscordMessageBuilder().AddEmbed(eb.Build());
                    }
                }

                try
                {
                    await logChannel.SendMessageAsync(mb);
                    Utils.Log($"[LOG] Sent anti-spam log with {imageData.Count} re-attached image(s) for {user.Username}.", guild.Name);
                }
                finally
                {
                    foreach (var (stream, _) in imageData)
                        stream.Dispose();
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"[ERROR] LogAction failed: {ex.Message}", guild.Name);
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1)
                return duration.TotalDays == 1 ? "1 day" : $"{duration.TotalDays:0} days";
            if (duration.TotalHours >= 1)
                return duration.TotalHours == 1 ? "1 hour" : $"{duration.TotalHours:0} hours";
            return duration.TotalMinutes == 1 ? "1 minute" : $"{duration.TotalMinutes:0} minutes";
        }
    }
}