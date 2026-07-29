using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

namespace UPBot
{
    /// <summary>
    /// Discord.Net's Color struct ships only a small set of named colors, nowhere near
    /// DSharpPlus's DiscordColor palette that the tag color picker relies on. This
    /// reimplements the same named entries as plain hex constants; the shades are close
    /// approximations of DSharpPlus's originals, not byte-for-byte identical.
    /// </summary>
    public static class TagColors
    {
        public static readonly Color Aquamarine = new(0x7FFFD4);
        public static readonly Color Azure = new(0x007FFF);
        public static readonly Color Blurple = new(0x5865F2);
        public static readonly Color Chartreuse = new(0x7FFF00);
        public static readonly Color CornflowerBlue = new(0x6495ED);
        public static readonly Color DarkBlue = new(0x00008B);
        public static readonly Color DarkButNotBlack = new(0x2C2F33);
        public static readonly Color DarkRed = new(0x7F0000);
        public static readonly Color Gold = new(0xFFD700);
        public static readonly Color Grayple = new(0x979C9F);
        public static readonly Color Green = new(0x57F287);
        public static readonly Color IndianRed = new(0xCD5C5C);
        public static readonly Color Lilac = new(0xB666D2);
        public static readonly Color MidnightBlue = new(0x191970);
        public static readonly Color NotQuiteBlack = new(0x23272A);
        public static readonly Color Orange = new(0xE67E22);
        public static readonly Color PhthaloBlue = new(0x000F89);
        public static readonly Color PhthaloGreen = new(0x123524);
        public static readonly Color Purple = new(0x800080);
        public static readonly Color Red = new(0xED4245);
        public static readonly Color Rose = new(0xFF007F);
        public static readonly Color SapGreen = new(0x507D2A);
        public static readonly Color Teal = new(0x1ABC9C);
        public static readonly Color Yellow = new(0xFEE75C);
    }

    /// <summary>
    /// Command that allows helpers, admins, etc. Add more information in "Help Language" script.
    /// Author: J0nathan550, CPU
    /// </summary>
    public class SlashTags : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("tag", "Show the contents of a specific tag (shows all the tags in case no tag is specified)")]
        public async Task TagCommand([Summary("tagname", "Tag to be shown")] string tagname = null)
        {
            Utils.LogUserCommand(Context);
            if (tagname != null)
            {
                try
                {
                    TagBase tag = FindTag(Context.Guild.Id, tagname.Trim(), true);
                    //EmbedBuilder embed = new();
                    var builder = new EmbedBuilder();
                    if (tag == null)
                    {
                        await RespondAsync(embed: builder.WithDescription($"{tagname} tag does not exist.").Build(), ephemeral: true);
                        return;
                    }
                    if (tag.ColorOfTheme == discordColors.Length)
                    {
                        int randomnumber = rand.Next(0, discordColors.Length);
                        builder.Color = discordColors[randomnumber];
                    }
                    else
                    {
                        builder.Color = discordColors[tag.ColorOfTheme];
                    }
                    builder.Timestamp = tag.timeOfCreation;
                    if (tag.thumbnailLink != null)
                    {
                        builder.ThumbnailUrl = $"{tag.thumbnailLink}";
                    }
                    if (tag.imageLink != null)
                    {
                        builder.ImageUrl = tag.imageLink;
                    }
                    else { }
                    if (tag != null)
                    {
                        builder.Title = tag.Topic;
                        builder.Author = tag.Author == "" || tag.AuthorIcon == ""
                            ? new EmbedAuthorBuilder
                            {
                                Name = "Unknown",
                                IconUrl = null
                            }
                            : new EmbedAuthorBuilder
                            {
                                Name = tag.Author,
                                IconUrl = tag.AuthorIcon
                            };
                        string descr = "";
                        if (tag.Alias3 != null) builder.Footer = new EmbedFooterBuilder { Text = $"Aliases: {CleanName(tag.Alias1)}, {CleanName(tag.Alias2)}, {CleanName(tag.Alias3)}" };
                        else if (tag.Alias2 != null) builder.Footer = new EmbedFooterBuilder { Text = $"Aliases: {CleanName(tag.Alias1)}, {CleanName(tag.Alias2)}" };
                        else if (tag.Alias1 != null) builder.Footer = new EmbedFooterBuilder { Text = $"Alias: {CleanName(tag.Alias1)}" };
                        descr += tag.Information;
                        await RespondAsync(embed: builder.WithDescription(descr).Build());
                    }
                    else
                    {
                        await RespondAsync(embed: builder.WithDescription($"{tagname} tag does not exist.").Build(), ephemeral: true);
                    }
                }
                catch (Exception ex)
                {
                    await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "Tag", ex));
                }
            }
            else
            {
                try
                {
                    EmbedBuilder embed = new();
                    string result = "";
                    if (Configs.Tags[Context.Guild.Id].Count == 0)
                    {
                        result = "No tags are defined. ";
                    }
                    else
                    {
                        int count = 0;
                        foreach (TagBase tag in Configs.Tags[Context.Guild.Id])
                        {
                            count++;
                            result += $"**{CleanName(tag.Topic)}**";
                            if (tag.Alias3 != null) result += $"Aliases: _**{CleanName(tag.Alias1)}**_, _**{CleanName(tag.Alias2)}**_, _**{CleanName(tag.Alias3)}**_";
                            else if (tag.Alias2 != null) result += $"Aliases: _**{CleanName(tag.Alias1)}**_, _**{CleanName(tag.Alias2)}**_";
                            else if (tag.Alias1 != null) result += $"Alias: _**{CleanName(tag.Alias1)}**_";
                            if (count < Configs.Tags[Context.Guild.Id].Count - 1) result += ", \n";
                            else result += ".";
                        }
                    }
                    embed.Title = "List of tags";
                    embed.Color = TagColors.Blurple;
                    embed.Description = result[..^2];
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build());
                }
                catch (Exception ex)
                {
                    await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagList", ex));
                }
            }
        }

        private static string CleanName(string name)
        {
            return name.Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`");
        }

        public static TagBase FindTag(ulong gid, string name, bool getClosest)
        {
            foreach (TagBase tag in Configs.Tags[gid])
            {
                if (name.Equals(tag.Topic, StringComparison.InvariantCultureIgnoreCase) ||
                  name.Equals(tag.Alias1, StringComparison.InvariantCultureIgnoreCase) ||
                  name.Equals(tag.Alias2, StringComparison.InvariantCultureIgnoreCase) ||
                  name.Equals(tag.Alias3, StringComparison.InvariantCultureIgnoreCase))
                {
                    return tag;
                }
            }
            if (getClosest)
            {
                // Try to find the closest one

                int min = int.MaxValue;
                TagBase res = null;
                foreach (TagBase tag in Configs.Tags[gid])
                {
                    int dist = StringDistance.Distance(name, tag.Topic);
                    if (min > dist)
                    {
                        min = dist;
                        res = tag;
                    }
                    if (tag.Alias1 != null)
                    {
                        dist = StringDistance.Distance(name, tag.Alias1);
                        if (min > dist)
                        {
                            min = dist;
                            res = tag;
                        }
                    }
                    if (tag.Alias2 != null)
                    {
                        dist = StringDistance.Distance(name, tag.Alias2);
                        if (min > dist)
                        {
                            min = dist;
                            res = tag;
                        }
                    }
                    if (tag.Alias3 != null)
                    {
                        dist = StringDistance.Distance(name, tag.Alias3);
                        if (min > dist)
                        {
                            min = dist;
                            res = tag;
                        }
                    }
                }
                if (min < 100)
                {
                    return res;
                }
            }

            return null;
        }

        private readonly Random rand = new();
        public static readonly Color[] discordColors = [
      TagColors.Aquamarine,
      TagColors.Azure,
      TagColors.Blurple,
      TagColors.Chartreuse,
      TagColors.CornflowerBlue,
      TagColors.DarkBlue,
      TagColors.DarkButNotBlack,
      TagColors.Gold,
      TagColors.Grayple,
      TagColors.Green,
      TagColors.IndianRed,
      TagColors.Lilac,
      TagColors.MidnightBlue,
      TagColors.NotQuiteBlack,
      TagColors.Orange,
      TagColors.PhthaloBlue,
      TagColors.PhthaloGreen,
      TagColors.Red,
      TagColors.Rose,
      TagColors.SapGreen,
      TagColors.Teal,
      TagColors.Yellow
  ];
    }

    public enum TagColorValue
    {
        [ChoiceDisplay("Aquamarine")] Aquamarine = 0,
        [ChoiceDisplay("Azure")] Azure = 1,
        [ChoiceDisplay("Blurple")] Blurple = 2,
        [ChoiceDisplay("Chartreuse")] Chartreuse = 3,
        [ChoiceDisplay("CornflowerBlue")] CornflowerBlue = 4,
        [ChoiceDisplay("DarkBlue")] DarkBlue = 5,
        [ChoiceDisplay("DarkButNotBlack")] DarkButNotBlack = 6,
        [ChoiceDisplay("Gold")] Gold = 7,
        [ChoiceDisplay("Grayple")] Grayple = 8,
        [ChoiceDisplay("Green")] Green = 9,
        [ChoiceDisplay("IndianRed")] IndianRed = 10,
        [ChoiceDisplay("Lilac")] Lilac = 11,
        [ChoiceDisplay("MidnightBlue")] MidnightBlue = 12,
        [ChoiceDisplay("NotQuiteBlack")] NotQuiteBlack = 13,
        [ChoiceDisplay("Orange")] Orange = 14,
        [ChoiceDisplay("PhthaloBlue")] PhthaloBlue = 15,
        [ChoiceDisplay("PhthaloGreen")] PhthaloGreen = 16,
        [ChoiceDisplay("Red")] Red = 17,
        [ChoiceDisplay("Rose")] Rose = 18,
        [ChoiceDisplay("SapGreen")] SapGreen = 19,
        [ChoiceDisplay("Teal")] Teal = 20,
        [ChoiceDisplay("Yellow")] Yellow = 21,
        [ChoiceDisplay("Random")] Random = 22
        //  [ChoiceDisplay("Sienna")] Sienna = 33,
        //  [ChoiceDisplay("HotPink")] HotPink = 19,
        //  [ChoiceDisplay("Black")] Black = 2,
        //  [ChoiceDisplay("Blue")] Blue = 3,
        //  [ChoiceDisplay("Brown")] Brown = 5,
        //  [ChoiceDisplay("Cyan")] Cyan = 8,
        //  [ChoiceDisplay("DarkGray")] DarkGray = 11,
        //  [ChoiceDisplay("DarkGreen")] DarkGreen = 12,
        //  [ChoiceDisplay("DarkRed")] DarkRed = 13,
        //  [ChoiceDisplay("Goldenrod")] Goldenrod = 15,
        //  [ChoiceDisplay("Gray")] Gray = 16,
        //  [ChoiceDisplay("LightGray")] LightGray = 21,
        //  [ChoiceDisplay("Magenta")] Magenta = 23,
        //  [ChoiceDisplay("Purple")] Purple = 29,
        //  [ChoiceDisplay("SpringGreen")] SpringGreen = 34,
        //  [ChoiceDisplay("Turquoise")] Turquoise = 36,
        //  [ChoiceDisplay("VeryDarkGray")] VeryDarkGray = 37,
        //  [ChoiceDisplay("Violet,")] Violet = 38,
        //  [ChoiceDisplay("Wheat")] Wheat = 39,
        //  [ChoiceDisplay("White")] White = 41,
    }

    [Group("tags", "Define and manage your tags")]
    public class SlashTagsEdit : InteractionModuleBase<SocketInteractionContext>
    {
        /// <summary>
        /// Discord.Net has no built-in "wait for the next message that matches a
        /// predicate" helper (that was DSharpPlus's Interactivity extension). This
        /// reimplements it with a one-shot subscription to MessageReceived.
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

        [SlashCommand("addtag", "Adds a new tag")]
        public async Task TagAddCommand([Summary("tagname", "Tag to be added")] string tagname)
        {
            Utils.LogUserCommand(Context);

            try
            {
                EmbedBuilder embed = new();
                tagname = tagname.Trim();

                foreach (var topics in Configs.Tags[Context.Guild.Id])
                {
                    if (tagname.Equals(topics.Topic, StringComparison.InvariantCultureIgnoreCase) ||
                        tagname.Equals(topics.Alias1, StringComparison.InvariantCultureIgnoreCase) ||
                        tagname.Equals(topics.Alias2, StringComparison.InvariantCultureIgnoreCase) ||
                        tagname.Equals(topics.Alias3, StringComparison.InvariantCultureIgnoreCase))
                    {
                        embed.Title = "The Tag exists already!";
                        embed.Color = TagColors.Red;
                        embed.Description = $"You are trying to add Tag {tagname} that already exists!\nIf you want to edit the Tag use: `tagedit <topic>` - to edit";
                        embed.Timestamp = DateTime.Now;
                        await RespondAsync(embed: embed.Build(), ephemeral: true);
                        return;
                    }
                }

                embed.Title = "Adding a Tag";
                embed.Color = TagColors.Green;
                embed.Description = $"Type the content of the Tag {tagname}.";
                embed.Timestamp = DateTime.Now;
                await RespondAsync(embed: embed.Build());

                var answer = await WaitForMessageAsync(dm => dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(5));

                if (answer == null)
                {
                    embed.Title = "Time expired!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"You took too much time to type the tag.";
                    embed.Timestamp = DateTime.Now;
                    await FollowupAsync(embed: embed.Build());
                    return;
                }
                TagBase tagBase = new(Context.Guild.Id, tagname, answer.Content, "", "", 22, DateTime.Now, null, null); // creating line inside of database
                Database.Add(tagBase); // adding information to base
                Configs.Tags[Context.Guild.Id].Add(tagBase);

                embed.Title = "Tag added";
                embed.Color = TagColors.Green;
                embed.Description = $"The topic: {tagname}, has been created";
                embed.Timestamp = DateTime.Now;
                await FollowupAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagAdd", ex));
            }
        }


        [SlashCommand("removetag", "Removes an existing tag")]
        public async Task TagRemoveCommand([Summary("tagname", "Tag to be removed")] string tagname)
        {
            Utils.LogUserCommand(Context);

            try
            {
                EmbedBuilder embed = new();

                TagBase toRemove = SlashTags.FindTag(Context.Guild.Id, tagname, false);
                if (toRemove == null)
                {
                    embed.Title = "The Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagname}` does not exist";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                Configs.Tags[Context.Guild.Id].Remove(toRemove);
                Database.DeleteByKeys<TagBase>(Context.Guild.Id, toRemove);

                embed.Title = "Topic deleted";
                embed.Color = TagColors.DarkRed;
                embed.Description = $"Tag `{tagname}` has been deleted by {((SocketGuildUser)Context.User).DisplayName}";
                embed.Timestamp = DateTime.Now;
                await RespondAsync(embed: embed.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagRemove", ex));
            }
        }

        [SlashCommand("listtags", "Shows all tags")]
        public async Task TagListCommand()
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                string result = "";
                if (Configs.Tags[Context.Guild.Id].Count == 0)
                {
                    result = "No tags are defined.";
                }
                else
                {
                    foreach (TagBase tag in Configs.Tags[Context.Guild.Id])
                    {
                        result += $"**{tag.Topic}**";
                        if (tag.Alias3 != null) result += $" (_**{tag.Alias1}**_, _**{tag.Alias2}**_, _**{tag.Alias3}**_)";
                        else if (tag.Alias2 != null) result += $" (_**{tag.Alias1}**_, _**{tag.Alias2}**_)";
                        else if (tag.Alias1 != null) result += $" (_**{tag.Alias1}**_)";
                        result += $",\n";
                    }
                }
                embed.Title = "List of tags";
                embed.Color = TagColors.Blurple;
                embed.Description = result[..^2];
                embed.Timestamp = DateTime.Now;
                await RespondAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagList", ex));
            }
        }

        [SlashCommand("aliastag", "Define aliases for a tag")]
        public async Task TagAliasCommand([Summary("tagname", "Tag to be aliased")] string tagname, [Summary("alias1", "First alias")] string alias1, [Summary("alias2", "Second alias")] string alias2 = null, [Summary("alias3", "Third alias")] string alias3 = null)
        {
            Utils.LogUserCommand(Context);

            try
            {
                EmbedBuilder embed = new();

                // Find it, can be an alias
                TagBase toAlias = SlashTags.FindTag(Context.Guild.Id, tagname, false);
                if (toAlias == null)
                {
                    embed.Title = "The Topic does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagname}` does not exist";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                // Check if we do not have the alias already
                if (alias1.Equals(toAlias.Topic, StringComparison.InvariantCultureIgnoreCase) || alias1.Equals(toAlias.Alias1, StringComparison.InvariantCultureIgnoreCase) ||
                    alias1.Equals(toAlias.Alias2, StringComparison.InvariantCultureIgnoreCase) || alias1.Equals(toAlias.Alias3, StringComparison.InvariantCultureIgnoreCase) ||
                    (alias2 != null && (alias2.Equals(toAlias.Topic, StringComparison.InvariantCultureIgnoreCase) || alias2.Equals(toAlias.Alias1, StringComparison.InvariantCultureIgnoreCase) ||
                                        alias2.Equals(toAlias.Alias2, StringComparison.InvariantCultureIgnoreCase) || alias2.Equals(toAlias.Alias3, StringComparison.InvariantCultureIgnoreCase))) ||
                    (alias3 != null && (alias3.Equals(toAlias.Topic, StringComparison.InvariantCultureIgnoreCase) || alias3.Equals(toAlias.Alias1, StringComparison.InvariantCultureIgnoreCase) ||
                                        alias3.Equals(toAlias.Alias2, StringComparison.InvariantCultureIgnoreCase) || alias3.Equals(toAlias.Alias3, StringComparison.InvariantCultureIgnoreCase))))
                {
                    embed.Title = "Alias already existing";
                    embed.Color = TagColors.Yellow;
                    embed.Description = $"Aliases for {toAlias.Topic.ToUpperInvariant()}:\n";
                    if (toAlias.Alias3 != null) embed.Description += $" (_**{toAlias.Alias1}**_, _**{toAlias.Alias2}**_, _**{toAlias.Alias3}**_)";
                    else if (toAlias.Alias2 != null) embed.Description += $" (_**{toAlias.Alias1}**_, _**{toAlias.Alias2}**_)";
                    else if (toAlias.Alias1 != null) embed.Description += $" (_**{toAlias.Alias1}**_)";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }

                // Find the first empty alias slot
                toAlias.Alias1 = alias1;
                toAlias.Alias2 = alias2;
                toAlias.Alias3 = alias3;
                Database.Add(toAlias);

                embed.Title = "Alias accepted";
                embed.Color = TagColors.Green;
                embed.Description = $"Aliases for {toAlias.Topic.ToUpperInvariant()}:\n";
                if (toAlias.Alias3 != null) embed.Description += $" (_**{toAlias.Alias1}**_, _**{toAlias.Alias2}**_, _**{toAlias.Alias3}**_)";
                else if (toAlias.Alias2 != null) embed.Description += $" (_**{toAlias.Alias1}**_, _**{toAlias.Alias2}**_)";
                else if (toAlias.Alias1 != null) embed.Description += $" (_**{toAlias.Alias1}**_)";
                embed.Timestamp = DateTime.Now;
                await RespondAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagAlias", ex));
            }
        }

        [SlashCommand("edittag", "Edit an existing tag")]
        public async Task TagEditCommand([Summary("tagname", "Tag to be modified")] string tagname)
        {
            Utils.LogUserCommand(Context);

            try
            {
                EmbedBuilder embed = new();

                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagname, false);
                if (toEdit == null)
                {
                    embed.Title = "The Topic does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagname}` does not exist";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }

                embed.Title = $"Editing {tagname}";
                embed.Color = TagColors.Purple;
                embed.Description = $"You are editing the {tagname.ToUpperInvariant()}.\nBetter to copy previous text, and edit inside of message.";
                embed.Timestamp = DateTime.Now;
                await RespondAsync(embed: embed.Build());

                var answer = await WaitForMessageAsync(dm => dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id, TimeSpan.FromMinutes(5));

                if (answer == null || string.IsNullOrWhiteSpace(answer.Content))
                {
                    embed.Title = "Time expired!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"You took too much time to answer. :KO:";
                    embed.Timestamp = DateTime.Now;
                    await FollowupAsync(embed: embed.Build());
                    return;
                }

                toEdit.Information = answer.Content;
                Database.Add(toEdit); // adding information to base

                embed.Title = "Changes accepted";
                embed.Color = TagColors.Green;
                embed.Description = $"New information for {tagname.ToUpperInvariant()}, is:\n\n{answer.Content}\n";
                embed.Timestamp = DateTime.Now;
                await RespondAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagEdit", ex));
            }
        }


        [SlashCommand("removealias", "Removes alias from tag")]
        public async Task TagRemoveAlias([Summary("tagname", "Tag to be modified")] string tagName)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                toEdit.Alias1 = null;
                toEdit.Alias2 = null;
                toEdit.Alias3 = null;
                Database.Add(toEdit); // adding information to base

                var builder = new EmbedBuilder()
                {
                    Title = "Alias Removed!",
                    Color = TagColors.Green,
                    Description = $"Removed Alias from: **'{tagName}'**!",
                    Timestamp = DateTime.Now,
                };
                await RespondAsync(embed: builder.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagThumbnail", ex));
            }
        }

        [SlashCommand("renametag", "Rename a tag")]
        public async Task TagRenameCommand([Summary("tagname", "Tag to be modified")] string oldname, [Summary("newname", "The new name for the tag")] string newname)
        {
            Utils.LogUserCommand(Context);

            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, oldname, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{oldname}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }

                toEdit.Topic = newname.Trim();
                Database.Add(toEdit); // adding information to base

                embed.Title = "Changes accepted";
                embed.Color = TagColors.Green;
                embed.Description = $"New name for {oldname.ToUpperInvariant()}, changed to:\n\n{newname}\n";
                embed.Timestamp = DateTime.Now;

                await RespondAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagRename", ex));
            }
        }

        [SlashCommand("addauthor", "Add author to the tag")]
        public async Task TagAddAuthor([Summary("tagname", "Tag to change the author")] string tagName, [Summary("authorname", "Pick author of tag")] string authorName)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }

                toEdit.Author = authorName.Trim();
                toEdit.AuthorIcon = ((SocketGuildUser)Context.User).GetDisplayAvatarUrl() ?? ((SocketGuildUser)Context.User).GetDefaultAvatarUrl();
                Database.Add(toEdit); // adding information to base

                embed.Title = "Changes accepted!";
                embed.Color = TagColors.Green;
                embed.Description = $"New author of tag: {tagName.ToUpperInvariant()}, is \n\n{authorName}\n";
                embed.Timestamp = DateTime.Now;

                await RespondAsync(embed: embed.Build());

            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagAddAuthor", ex));
            }
        }

        [SlashCommand("addcolor", "Add color scheme to tag")]
        public async Task TagColorPicking([Summary("tagname", "Tag to set the color")] string tagName, [Summary("colorname", "just a comment")] TagColorValue? colorName = null)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                int colorNumber = (int)colorName;
                if (colorNumber <= SlashTags.discordColors.Length)
                {
                    toEdit.ColorOfTheme = colorNumber;
                    Database.Add(toEdit); // adding information to base

                    embed.Title = "Changes accepted!";
                    embed.Color = TagColors.Green;
                    embed.Description = $"New color for tag: {tagName.ToUpperInvariant()}, is \n{colorName} {SlashTags.discordColors[colorNumber]} - id {colorNumber}.";
                    if (colorNumber == SlashTags.discordColors.Length)
                        embed.Description = $"New color for tag: {tagName.ToUpperInvariant()}, is \n_random color_ (id {colorNumber}).";
                    else
                        embed.Timestamp = DateTime.Now;

                    await RespondAsync(embed: embed.Build());
                }
                else
                {
                    embed.Title = "Color id does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"Color id: {colorNumber} does not exist. Pick onve of the dropdown values!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagColor", ex));
            }
        }

        [SlashCommand("addimage", "Add a image to the tag")]
        public async Task TagImagePicking([Summary("tagname", "Tag to add the thumbnail")] string tagName, [Summary("image", "Link to image")] string imageLink)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                toEdit.imageLink = imageLink;
                Database.Add(toEdit); // adding information to base

                var builder = new EmbedBuilder
                {
                    Title = "Changes accepted!",
                    Color = TagColors.Green,
                    ImageUrl = toEdit.imageLink,
                    Description = $"New Image link for tag: {tagName}, is \n{imageLink}.",
                    Timestamp = DateTime.Now
                };
                await RespondAsync(embed: builder.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagImage", ex));
            }
        }

        [SlashCommand("removeimage", "Remove image from the tag")]
        public async Task TagImageRemoving([Summary("tagname", "Tag with thumbnail")] string tagName)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                if (string.IsNullOrEmpty(toEdit.thumbnailLink))
                {
                    embed.Title = "Tag does not have any Thumbnail!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"Tag does not have any Thumbnail!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                toEdit.thumbnailLink = null;
                Database.Add(toEdit); // adding information to base

                var builder = new EmbedBuilder()
                {
                    Title = "Image Removed!",
                    Color = TagColors.Green,
                    Description = $"Removed Image from: **'{tagName}'**!",
                    Timestamp = DateTime.Now,
                };
                await RespondAsync(embed: builder.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagImage", ex));
            }
        }

        [SlashCommand("addthumbnail", "Add a thumbnail image to the tag")]
        public async Task TagThumbnailPicking([Summary("tagname", "Tag to add the thumbnail")] string tagName, [Summary("thumbnail", "Link to image")] string thumbnailLink)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                toEdit.thumbnailLink = thumbnailLink;
                Database.Add(toEdit); // adding information to base

                var builder = new EmbedBuilder
                {
                    Title = "Changes accepted!",
                    Color = TagColors.Green,
                    ThumbnailUrl = $"{thumbnailLink}",
                    Description = $"New Thumbnail link for tag: {tagName}, is \n{thumbnailLink}.",
                    Timestamp = DateTime.Now
                };
                await RespondAsync(embed: builder.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagThumbnail", ex));
            }
        }

        [SlashCommand("removethumbnail", "Remove the thumbnail image from the tag")]
        public async Task TagThumbnailRemoving([Summary("tagname", "Tag with thumbnail")] string tagName)
        {
            Utils.LogUserCommand(Context);
            try
            {
                EmbedBuilder embed = new();
                TagBase toEdit = SlashTags.FindTag(Context.Guild.Id, tagName, false);
                if (toEdit == null)
                {
                    embed.Title = "Tag does not exist!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"The tag `{tagName}` does not exist!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                if (string.IsNullOrEmpty(toEdit.thumbnailLink))
                {
                    embed.Title = "Tag does not have any Thumbnail!";
                    embed.Color = TagColors.Red;
                    embed.Description = $"Tag does not have any Thumbnail!";
                    embed.Timestamp = DateTime.Now;
                    await RespondAsync(embed: embed.Build(), ephemeral: true);
                    return;
                }
                toEdit.thumbnailLink = null;
                Database.Add(toEdit); // adding information to base

                var builder = new EmbedBuilder()
                {
                    Title = "Thumbnail Removed!",
                    Color = TagColors.Green,
                    Description = $"Removed Thumbnail from: **'{tagName}'**!",
                    Timestamp = DateTime.Now,
                };
                await RespondAsync(embed: builder.Build());
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Utils.GenerateErrorAnswer(Context.Guild.Name, "TagThumbnail", ex));
            }
        }

    }
}