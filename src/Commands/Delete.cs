using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

namespace UPBot
{
    public class SlashDelete : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("massdel", "Deletes all the last messages (massdel 10) or from a user (massdel @User 10) in the channel")]
        public async Task DeleteCommand(
            [Summary("count", "How many messages to delete")][MinValue(1)][MaxValue(50)] long count,
            [Summary("user", "What user's messages to delete")] IUser user = null)
        {
            // Check permissions
            SocketGuildUser invoker = Context.User as SocketGuildUser;
            if (!Configs.HasAdminRole(Context.Guild.Id, invoker.Roles, false))
            {
                Utils.DefaultNotAllowed(Context);
                return;
            }

            Utils.LogUserCommand(Context);

            // Validate count
            if (count <= 0 || count > 50)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "WhatLanguage",
                    $"Invalid message count: {count}. Must be between 1 and 50."));
                return;
            }

            // Acknowledge the command
            await RespondAsync("🗑️ Starting deletion process...");

            try
            {
                // Fetch messages from the channel
                var allMessages = new List<IMessage>();
                var messagesToDelete = new List<IMessage>();

                ITextChannel channel = Context.Channel as ITextChannel;

                // Get more messages than needed to account for filtering
                int fetchLimit = user == null ? (int)count + 10 : Math.Min(200, (int)count * 3);
                allMessages.AddRange((await channel.GetMessagesAsync(fetchLimit).FlattenAsync()));

                // Filter messages based on criteria
                var filteredMessages = allMessages.Where(m =>
                {
                    // Skip the bot's own response message
                    if (m.Author.Id == Context.Client.CurrentUser.Id &&
                        m.Content.Contains("Starting deletion process")) return false;

                    // If user is specified, only include their messages
                    if (user != null && m.Author.Id != user.Id) return false;

                    return true;
                }).Take((int)count).ToList();

                if (filteredMessages.Count == 0)
                {
                    await ModifyOriginalResponseAsync(m => m.Content = "❌ No messages found to delete.");
                    return;
                }

                // Separate messages by age (Discord bulk delete limitation)
                var cutoffTime = DateTimeOffset.UtcNow.AddDays(-14);
                var recentMessages = filteredMessages.Where(m => m.Timestamp > cutoffTime).ToList();
                var oldMessages = filteredMessages.Where(m => m.Timestamp <= cutoffTime).ToList();

                int totalDeleted = 0;

                // Delete recent messages in bulk (more efficient)
                if (recentMessages.Count > 0)
                {
                    if (recentMessages.Count == 1)
                    {
                        // Single message - use individual delete
                        await recentMessages[0].DeleteAsync();
                        totalDeleted++;
                    }
                    else
                    {
                        // Multiple messages - use bulk delete
                        await channel.DeleteMessagesAsync(recentMessages);
                        totalDeleted += recentMessages.Count;
                    }
                }

                // Delete old messages individually (Discord requirement)
                foreach (var oldMessage in oldMessages)
                {
                    try
                    {
                        await oldMessage.DeleteAsync();
                        totalDeleted++;

                        // Small delay to avoid rate limiting
                        if (oldMessages.Count > 5)
                            await Task.Delay(250);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log($"Failed to delete old message: {ex.Message}", Context.Guild.Name);
                        // Continue with other messages
                    }
                }

                // Update response with results
                string resultMessage = user != null
                    ? $"✅ Deleted {totalDeleted} messages from {user.Username}"
                    : $"✅ Deleted {totalDeleted} messages";

                // Try to edit the response, if it fails, send a new message
                try
                {
                    await ModifyOriginalResponseAsync(m => m.Content = resultMessage);

                    // Delete the success message after a few seconds
                    await Task.Delay(3000);
                    try
                    {
                        var response = await GetOriginalResponseAsync();
                        await response.DeleteAsync();
                    }
                    catch { } // Ignore if already deleted
                }
                catch
                {
                    // If editing fails, send a new message
                    var successMsg = await channel.SendMessageAsync(resultMessage);
                    await Task.Delay(3000);
                    try
                    {
                        await successMsg.DeleteAsync();
                    }
                    catch { } // Ignore if already deleted
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"Delete command error: {ex.Message}", Context.Guild.Name);

                try
                {
                    await ModifyOriginalResponseAsync(m => m.Content = $"❌ Error during deletion: {ex.Message}");
                }
                catch
                {
                    await (Context.Channel as ITextChannel).SendMessageAsync($"❌ Error during deletion: {ex.Message}");
                }
            }
        }
    }
}
