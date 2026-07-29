using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UPBot;
using UPBot.UPBot_Code;


/// <summary>
/// This command implements simple games like:
/// Rock-Paper-Scissors, Coin Flip, Tic-Tac-Toe
/// author: SlicEnDicE, J0nathan550
/// </summary>

[Group("game", "Commands to play games with the bot")]
public class SlashGame : InteractionModuleBase<SocketInteractionContext>
{
    private readonly Random random = new();

    /// <summary>
    /// Discord.Net has no built-in "wait for the next message that matches a predicate"
    /// helper (DSharpPlus's Interactivity extension provided this). Reimplemented with a
    /// one-shot subscription to MessageReceived.
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
    /// Same story for "wait for a button press on this message" (DSharpPlus's
    /// WaitForButtonAsync). Subscribes to ButtonExecuted, matches on message id, and
    /// acknowledges the interaction (Defer) before returning its custom id.
    /// </summary>
    private static async Task<string> WaitForButtonAsync(IUserMessage message, TimeSpan timeout)
    {
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
            if (completed != tcs.Task) return null;
            var comp = tcs.Task.Result;
            await comp.DeferAsync();
            return comp.Data.CustomId;
        }
        finally
        {
            client.ButtonExecuted -= Handler;
        }
    }

    [SlashCommand("rockpaperscissors", "Play Rock, Paper, Scissors")]
    public async Task RPSCommand([Summary("yourmove", "Rock, Paper, or Scissors")] RPSTypes? yourmove = null)
    {
        Utils.LogUserCommand(Context);

        RPSTypes botChoice = (RPSTypes)random.Next(0, 3);
        if (yourmove != null)
        {
            if (yourmove == RPSTypes.Rock)
            {
                if (botChoice == RPSTypes.Rock)
                {
                    await RespondAsync($"You said 🪨 Rock {Context.User.Mention}, I played 🪨 Rock! **DRAW!**");
                }
                else if (botChoice == RPSTypes.Paper)
                {
                    await RespondAsync($"You said 🪨 Rock {Context.User.Mention}, I played 📄 Paper! **I win!**");
                }
                else
                {
                    await RespondAsync($"You said 🪨 Rock {Context.User.Mention}, I played ✂️ Scissor! **You win!**");
                }
            }
            else if (yourmove == RPSTypes.Paper)
            {
                if (botChoice == RPSTypes.Rock)
                {
                    await RespondAsync($"You said 📄 Paper {Context.User.Mention}, I played 🪨 Rock! **You win!**");
                }
                else if (botChoice == RPSTypes.Paper)
                {
                    await RespondAsync($"You said 📄 Paper {Context.User.Mention}, I played 📄 Paper! **DRAW!**");
                }
                else
                {
                    await RespondAsync($"You said 📄 Paper {Context.User.Mention}, I played ✂️ Scissor! **I win!**");
                }
            }
            else
            {
                if (botChoice == RPSTypes.Rock)
                {
                    await RespondAsync($"You said ✂️ Scissor {Context.User.Mention}, I played 🪨 Rock! **I win!**");
                }
                else if (botChoice == RPSTypes.Paper)
                {
                    await RespondAsync($"You said ✂️ Scissor {Context.User.Mention}, I played 📄 Paper! **You win!**");
                }
                else
                {
                    await RespondAsync($"You said ✂️ Scissor {Context.User.Mention}, I played ✂️ Scissor! **DRAW!**");
                }
            }
            return;
        }

        await RespondAsync("Pick your move");

        var components = new ComponentBuilder()
            .WithButton("🪨 Rock", "bRock", ButtonStyle.Primary)
            .WithButton("📄 Paper", "bPaper", ButtonStyle.Primary)
            .WithButton("✂️ Scissors", "bScissors", ButtonStyle.Primary);

        IMessageChannel channel = Context.Channel;
        IUserMessage msg = await channel.SendMessageAsync("Select 🪨, 📄, or ✂️", components: components.Build());
        string buttonId = await WaitForButtonAsync(msg, TimeSpan.FromMinutes(2));
        if (buttonId != null)
        {
            if (buttonId == "bRock")
            {
                if (botChoice == RPSTypes.Rock)
                {
                    await channel.SendMessageAsync($"You said 🪨 Rock {Context.User.Mention}, I played 🪨 Rock! **DRAW!**");
                }
                else if (botChoice == RPSTypes.Paper)
                {
                    await channel.SendMessageAsync($"You said 🪨 Rock {Context.User.Mention}, I played 📄 Paper! **I win!**");
                }
                else
                {
                    await channel.SendMessageAsync($"You said 🪨 Rock {Context.User.Mention}, I played ✂️ Scissor! **You win!**");
                }
            }
            else if (buttonId == "bPaper")
            {
                if (botChoice == RPSTypes.Rock)
                {
                    await channel.SendMessageAsync($"You said 📄 Paper {Context.User.Mention}, I played 🪨 Rock! **You win!**");
                }
                else if (botChoice == RPSTypes.Paper)
                {
                    await channel.SendMessageAsync($"You said 📄 Paper {Context.User.Mention}, I played 📄 Paper! **DRAW!**");
                }
                else
                {
                    await channel.SendMessageAsync($"You said 📄 Paper {Context.User.Mention}, I played ✂️ Scissor! **I win!**");
                }
            }
            else if (buttonId == "bScissors")
            {
                await channel.SendMessageAsync($"You said ✂️ Scissor {Context.User.Mention}, I played 🪨 Rock! **I win!**");
            }
            else if (botChoice == RPSTypes.Paper)
            {
                await channel.SendMessageAsync($"You said ✂️ Scissor {Context.User.Mention}, I played 📄 Paper! **You win!**");
            }
            else
            {
                await channel.SendMessageAsync($"You said ✂️ Scissor {Context.User.Mention}, I played ✂️ Scissor! **DRAW!**");
            }
        }
        await msg.DeleteAsync();
    }

    public enum RPSTypes
    { // 🪨📄
        [ChoiceDisplay("Rock")] Rock = 0,
        [ChoiceDisplay("Paper")] Paper = 1,
        [ChoiceDisplay("Scissors")] Scissors = 2
    }
    public enum RPSLSTypes
    { // 🪨📄✂️🦎🖖
        [ChoiceDisplay("🪨 Rock")] Rock = 0,
        [ChoiceDisplay("📄 Paper")] Paper = 1,
        [ChoiceDisplay("✂️ Scissors")] Scissors = 2,
        [ChoiceDisplay("🦎 Lizard")] Lizard = 3,
        [ChoiceDisplay("🖖 Spock")] Spock = 4
    }
    private enum RPSRes { First, Second, Draw }
    private readonly RPSRes[][] rpslsRes = [
    //                                  Rock          Paper         Scissors         Lizard         Spock 
    /* Rock     */ [RPSRes.Draw,   RPSRes.Second,  RPSRes.First,  RPSRes.First,  RPSRes.Second],
    /* Paper    */ [RPSRes.First,  RPSRes.Draw,    RPSRes.Second, RPSRes.Second, RPSRes.First],
    /* Scissors */ [RPSRes.Second, RPSRes.First,   RPSRes.Draw,   RPSRes.First,  RPSRes.Second],
    /* Lizard   */ [RPSRes.Second, RPSRes.First,   RPSRes.Second, RPSRes.Draw,   RPSRes.First],
    /* Spock    */ [RPSRes.First,  RPSRes.Second,  RPSRes.First,  RPSRes.Second, RPSRes.Draw]
  ];
    private readonly string[][] rpslsMsgs = [
    //                            Rock                    Paper                     Scissors                        Lizard                          Spock 
    /* Rock     */ ["Draw",                  "Paper covers Rock",      "rock crushes scissors",        "Rock crushes Lizard",          "Spock vaporizes Rock"],
    /* Paper    */ ["Paper covers Rock",     "Draw",                   "Scissors cuts Paper",          "Lizard eats Paper",            "Paper disproves Spock"],
    /* Scissors */ ["Rock crushes scissors", "Scissors cuts Paper",    "Draw",                         "Scissors decapitates Lizard",  "Spock smashes Scissors"],
    /* Lizard   */ ["Rock crushes Lizard",   "Lizard eats Paper",      "Scissors decapitates Lizard",  "Draw",                         "Lizard poisons Spock"],
    /* Spock    */ ["Spock vaporizes Rock",  "Paper disproves Spock",  "Spock smashes Scissors",       "Lizard poisons Spock",         "Draw"]
  ];

    private static string GetChoice(RPSLSTypes? move)
    {
        return move switch
        {
            RPSLSTypes.Rock => "🪨 Rock",
            RPSLSTypes.Paper => "📄 Paper",
            RPSLSTypes.Scissors => "✂️ Scissors",
            RPSLSTypes.Lizard => "🦎 Lizard",
            RPSLSTypes.Spock => "🖖 Spock",
            _ => "?",
        };
    }


    [SlashCommand("rockpaperscissorslizardspock", "Play Rock, Paper, Scissors, Lizard, Spock")]
    public async Task RPSLKCommand([Summary("yourmove", "Rock, Paper, or Scissors")] RPSLSTypes? yourmove = null)
    {
        Utils.LogUserCommand(Context);

        RPSLSTypes botChoice = (RPSLSTypes)random.Next(0, 5);
        if (yourmove != null)
        {
            string resmsg = rpslsMsgs[(int)yourmove][(int)botChoice];
            switch (rpslsRes[(int)yourmove][(int)botChoice])
            {
                case RPSRes.First:
                    await RespondAsync($"You said {GetChoice(yourmove)} {Context.User.Mention}, I played {GetChoice(botChoice)}! {resmsg} **You win!**");
                    break;
                case RPSRes.Second:
                    await RespondAsync($"You said {GetChoice(yourmove)} {Context.User.Mention}, I played {GetChoice(botChoice)}! {resmsg} **I win!**");
                    break;
                case RPSRes.Draw:
                    await RespondAsync($"You said {GetChoice(yourmove)} {Context.User.Mention}, I played {GetChoice(botChoice)}! **DRAW!**");
                    break;
            }
            return;
        }

        await RespondAsync("Pick your move");


        var components = new ComponentBuilder()
            .WithButton("🪨 Rock", "bRock", ButtonStyle.Primary)
            .WithButton("📄 Paper", "bPaper", ButtonStyle.Primary)
            .WithButton("✂️ Scissors", "bScissors", ButtonStyle.Primary)
            .WithButton("🦎 Lizard", "bLizard", ButtonStyle.Primary)
            .WithButton("🖖 Spock", "bSpock", ButtonStyle.Primary);

        IMessageChannel channel = Context.Channel;
        IUserMessage msg = await channel.SendMessageAsync("Select 🪨, 📄, ✂️, 🦎, or 🖖", components: components.Build());
        string buttonId = await WaitForButtonAsync(msg, TimeSpan.FromMinutes(2));
        if (buttonId != null)
        {
            yourmove = buttonId switch
            {
                "bRock" => RPSLSTypes.Rock,
                "bPaper" => RPSLSTypes.Paper,
                "bScissors" => RPSLSTypes.Scissors,
                "bLizard" => RPSLSTypes.Lizard,
                "bSpock" => RPSLSTypes.Spock,
                _ => yourmove
            };
            string resmsg = rpslsMsgs[(int)yourmove][(int)botChoice];
            switch (rpslsRes[(int)yourmove][(int)botChoice])
            {
                case RPSRes.First:
                    await channel.SendMessageAsync($"You said {GetChoice(yourmove)} {Context.User.Mention}, I played {GetChoice(botChoice)}! {resmsg}: **You win!**");
                    break;
                case RPSRes.Second:
                    await channel.SendMessageAsync($"You said {GetChoice(yourmove)} {Context.User.Mention}, I played {GetChoice(botChoice)}! {resmsg}: **I win!**");
                    break;
                case RPSRes.Draw:
                    await channel.SendMessageAsync($"You said {GetChoice(yourmove)} {Context.User.Mention}, I played {GetChoice(botChoice)}! **DRAW!**");
                    break;
            }
        }
        await msg.DeleteAsync(); // Expired
    }
    [SlashCommand("coin", "Flip a coin, to deside your choice!")]

    public async Task CoinFlipCommand([Summary("firstoption", "Optional: You have to do this is the coin is Head")] string firstOption = null, [Summary("secondoption", "Optional: You have to do this is the coin is Tails")] string secondOption = null)
    {
        Utils.LogUserCommand(Context);
        int randomNumber;
        if (firstOption == null || secondOption == null)
        {
            randomNumber = random.Next(0, 2);
            switch (randomNumber)
            {
                case 0:
                    var builder = new EmbedBuilder
                    {
                        Title = "Coin Flip!",
                        Color = TagColors.Yellow,
                        ThumbnailUrl = "https://emojipedia-us.s3.dualstack.us-west-1.amazonaws.com/thumbs/120/apple/325/coin_1fa99.png",
                        Description = "Heads on the coin!",
                        Timestamp = DateTime.Now
                    };
                    await RespondAsync(embed: builder.Build());
                    break;
                case 1:
                    var builder1 = new EmbedBuilder
                    {
                        Title = "Coin Flip!",
                        Color = TagColors.Yellow,
                        ThumbnailUrl = "https://emojipedia-us.s3.dualstack.us-west-1.amazonaws.com/thumbs/160/samsung/265/coin_1fa99.png",
                        Description = "Tails on the coin!",
                        Timestamp = DateTime.Now
                    };
                    await RespondAsync(embed: builder1.Build());
                    break;
            }
            return;
        }
        randomNumber = random.Next(0, 2);
        switch (randomNumber)
        {
            case 0:
                var builder2 = new EmbedBuilder
                {
                    Title = "Coin Flip!",
                    Color = TagColors.Yellow,
                    ThumbnailUrl = "https://emojipedia-us.s3.dualstack.us-west-1.amazonaws.com/thumbs/120/apple/325/coin_1fa99.png",
                    Description = "Heads on the coin!\n" +
                    $"You have to: **{firstOption}**",
                    Timestamp = DateTime.Now
                };
                await RespondAsync(embed: builder2.Build());
                break;
            case 1:
                var builder3 = new EmbedBuilder
                {
                    Title = "Coin Flip!",
                    Color = TagColors.Yellow,
                    ThumbnailUrl = "https://emojipedia-us.s3.dualstack.us-west-1.amazonaws.com/thumbs/160/samsung/265/coin_1fa99.png",
                    Description = "Tails on the coin!\n" +
                    $"You have to: **{secondOption}**",
                    Timestamp = DateTime.Now
                };
                await RespondAsync(embed: builder3.Build());
                break;
        }
    }


    private static string PrintBoard(int[] grid)
    {
        string board = "";
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                int pos = x + 3 * y;
                board += grid[pos] switch
                {
                    1 => ":o:",
                    2 => ":x:",
                    _ => ":black_large_square:"
                };
                board += "¹²³⁴⁵⁶⁷⁸⁹"[pos];
            }
            board += "\n";
        }
        return board;
    }

    private static readonly List<ulong> tttPlayers = [];
    public static void CleanupTicTacToe()
    {
        tttPlayers.Clear();
    }

    [SlashCommand("tictactoe", "Play Tic-Tac-Toe game with someone or aganinst the bot.")]
    public async Task TicTacToeGame([Summary("opponent", "Select a Discord user to play with (keep empty to play with the bot)")] IUser opponent = null)
    {
        Utils.LogUserCommand(Context);
        int[] grid = [0, 0, 0, 0, 0, 0, 0, 0, 0];
        SocketGuildUser player = Context.User as SocketGuildUser;
        bool oMoves = true;

        IMessageChannel channel = Context.Channel;

        // Game loop
        try
        {
            bool firstDone = false;
            EmbedBuilder message = new();
            if (opponent == null || opponent.Id == Utils.GetClient().CurrentUser.Id || opponent.Id == Context.User.Id)
            {
                if (tttPlayers.Contains(Context.User.Id))
                {
                    message.Title = $"You are already playing Tic-Tac-Toe!\n{player.DisplayName}";
                    message.Color = TagColors.Red;
                    await RespondAsync(embed: message.Build());
                    return;
                }
                tttPlayers.Add(Context.User.Id);
            }
            else
            {
                if (tttPlayers.Contains(Context.User.Id))
                {
                    message.Title = $"You are already playing Tic-Tac-Toe!\n{player.DisplayName}";
                    message.Color = TagColors.Red;
                    await RespondAsync(embed: message.Build());
                    return;
                }
                if (tttPlayers.Contains(opponent.Id))
                {
                    message.Title = $"{opponent.Username} is already playing Tic-Tac-Toe!";
                    message.Color = TagColors.Red;
                    await RespondAsync(embed: message.Build());
                    return;
                }
                tttPlayers.Add(opponent.Id);
                tttPlayers.Add(Context.User.Id);

                message.Description = $"**Playing with {opponent.Mention}**";
                message.Title = $"Tic-Tac-Toe Game {player.DisplayName}/{opponent.Username}";
                message.Timestamp = DateTime.Now;
                message.Color = TagColors.Red;
                await RespondAsync(embed: message.Build());
                firstDone = true;
            }


            IUserMessage board = null;
            while (true)
            {

                // Print the board

                message = new EmbedBuilder();
                if (opponent == null || opponent.Id == Utils.GetClient().CurrentUser.Id || opponent.Id == Context.User.Id)
                {
                    message.Title = $"Tic-Tac-Toe Game {player.DisplayName}/Bot";
                    if (oMoves) message.Description = $"{player.DisplayName}: Type a number between 1 and 9 to make a move.\n\n{PrintBoard(grid)}";
                    // no need to print the board for the bot
                    message.Timestamp = DateTime.Now;
                    message.Color = TagColors.Red;
                }
                else
                {
                    message.Description = oMoves
                        ? $"{opponent.Username}: Type a number between 1 and 9 to make a move.\n\n" + PrintBoard(grid)
                        : $"{player.DisplayName}: Type a number between 1 and 9 to make a move.\n\n" + PrintBoard(grid);
                    message.Title = $"Tic-Tac-Toe Game {player.DisplayName}/{opponent.Username}";
                    message.Timestamp = DateTime.Now;
                    message.Color = TagColors.Red;
                }
                if (oMoves || (opponent != null && opponent.Id != Utils.GetClient().CurrentUser.Id && opponent.Id != Context.User.Id))
                {
                    if (board != null) await board.DeleteAsync();

                    if (firstDone)
                        board = await FollowupAsync(embed: message.Build());
                    else
                    {
                        await RespondAsync(embed: message.Build());
                        firstDone = true;
                    }
                }

                if (oMoves || opponent != null)
                { // Get the answer from the current user
                    var answer = await WaitForMessageAsync(dm => opponent == null || !oMoves ?
                      dm.Channel.Id == Context.Channel.Id && dm.Author.Id == Context.User.Id : dm.Channel.Id == Context.Channel.Id && dm.Author.Id == opponent.Id, TimeSpan.FromMinutes(1));

                    if (answer == null)
                    {
                        message = new EmbedBuilder
                        {
                            Title = "Time expired!",
                            Color = TagColors.Red,
                            Description = $"You took too much time to type your move. Game is ended!",
                            Timestamp = DateTime.Now
                        };
                        if (board != null) await board.DeleteAsync();
                        await FollowupAsync(embed: message.Build());

                        if (opponent != null) tttPlayers.Remove(opponent.Id);
                        tttPlayers.Remove(Context.User.Id);
                        return;
                    }

                    if (int.TryParse(answer.Content, out var cell))
                    {
                        if (cell < 1 || cell > 9) continue;
                        cell--;
                        if (grid[cell] != 0) continue;

                        grid[cell] = oMoves ? 1 : 2;
                    }
                    else continue;

                }
                else
                { // Bot move
                    BotPick(grid);
                }

                // Check victory
                bool oWins = false;
                bool xWins = false;
                for (int i = 0; i < 3 && !oWins && !xWins; i++)
                {
                    if (grid[i * 3 + 0] == 1 && grid[i * 3 + 1] == 1 && grid[i * 3 + 2] == 1)
                    {
                        oWins = true;
                        break;
                    }
                    if (grid[i * 3 + 0] == 2 && grid[i * 3 + 1] == 2 && grid[i * 3 + 2] == 2)
                    {
                        xWins = true;
                        break;
                    }
                }
                for (int i = 0; i < 3 && !oWins && !xWins; i++)
                {
                    if (grid[0 * 3 + i] == 1 && grid[1 * 3 + i] == 1 && grid[2 * 3 + i] == 1)
                    {
                        oWins = true;
                        break;
                    }
                    if (grid[0 * 3 + i] == 2 && grid[1 * 3 + i] == 2 && grid[2 * 3 + i] == 2)
                    {
                        xWins = true;
                        break;
                    }
                }
                if (grid[0] == 1 && grid[4] == 1 && grid[8] == 1)
                {
                    oWins = true;
                }
                if (grid[2] == 1 && grid[4] == 1 && grid[6] == 1)
                {
                    oWins = true;
                }
                if (grid[0] == 2 && grid[4] == 2 && grid[8] == 2)
                {
                    xWins = true;
                }
                if (grid[2] == 2 && grid[4] == 2 && grid[6] == 2)
                {
                    xWins = true;
                }

                if (oWins)
                {
                    message = new EmbedBuilder
                    {
                        Title = $"Tic-Tac-Toe Game: :o: ({(opponent == null ? player.Username : opponent.Username)}) Wins!",
                        Description = $"**Game is ended!**\n\n{PrintBoard(grid)}",
                        Color = TagColors.Red,
                        Timestamp = DateTime.Now
                    };
                    if (board != null) await board.DeleteAsync();
                    await FollowupAsync(embed: message.Build());
                    if (opponent != null) tttPlayers.Remove(opponent.Id);
                    tttPlayers.Remove(Context.User.Id);
                    return;
                }
                if (xWins)
                {
                    message = new EmbedBuilder
                    {
                        Title = opponent == null ? $"Tic-Tac-Toe Game: :x: (Bot) Wins!" : $"Tic-Tac-Toe Game: :x: ({player.Username}) Wins!",
                        Description = $"**Game is ended!**\n\n{PrintBoard(grid)}",
                        Color = TagColors.Red,
                        Timestamp = DateTime.Now
                    };
                    if (board != null) await board.DeleteAsync();
                    await FollowupAsync(embed: message.Build());
                    if (opponent != null) tttPlayers.Remove(opponent.Id);
                    tttPlayers.Remove(Context.User.Id);
                    return;
                }

                // Draw?
                bool draw = true;
                for (int i = 0; i < 9; i++)
                {
                    if (grid[i] == 0)
                    {
                        draw = false;
                        break;
                    }
                }
                if (draw)
                {
                    message = new EmbedBuilder
                    {
                        Title = "Tic-Tac-Toe Game: Draw!",
                        Description = $"**Game is ended!**\n\n{PrintBoard(grid)}",
                        Color = TagColors.Red,
                        Timestamp = DateTime.Now
                    };
                    if (board != null) await board.DeleteAsync();
                    await FollowupAsync(embed: message.Build());
                    if (opponent != null) tttPlayers.Remove(opponent.Id);
                    tttPlayers.Remove(Context.User.Id);
                    return;
                }


                // Make the other one move
                oMoves = !oMoves;
            }
        }
        catch (Exception ex)
        {
            Utils.Log(ex.Message, null);
        }
    }

    private static void BotPick(int[] grid)
    {
        int pos = -1;

        // Check if the center is used, if not pick it.
        if (grid[4] == 0)
        {
            grid[4] = 2;
            return;
        }

        // Check if there are at least 2 positions in sequence, in case block it or win it
        for (int c = 0; c < 3 && pos == -1; c++)
        {
            int r = 3 * c;
            if (grid[0 + r] == 0 && grid[1 + r] == 2 && grid[2 + r] == 2) pos = r;
            if (grid[0 + r] == 2 && grid[1 + r] == 0 && grid[2 + r] == 2) pos = r + 1;
            if (grid[0 + r] == 2 && grid[1 + r] == 2 && grid[2 + r] == 0) pos = r + 2;

            if (grid[0 + r] == 0 && grid[1 + r] == 1 && grid[2 + r] == 1) pos = r;
            if (grid[0 + r] == 1 && grid[1 + r] == 0 && grid[2 + r] == 1) pos = r + 1;
            if (grid[0 + r] == 1 && grid[1 + r] == 1 && grid[2 + r] == 0) pos = r + 2;

            if (grid[c] == 0 && grid[c + 3] == 2 && grid[c + 6] == 2) pos = c;
            if (grid[c] == 2 && grid[c + 3] == 0 && grid[c + 6] == 2) pos = c + 3;
            if (grid[c] == 2 && grid[c + 3] == 2 && grid[c + 6] == 0) pos = c + 6;

            if (grid[c] == 0 && grid[c + 3] == 1 && grid[c + 6] == 1) pos = c;
            if (grid[c] == 1 && grid[c + 3] == 0 && grid[c + 6] == 1) pos = c + 3;
            if (grid[c] == 1 && grid[c + 3] == 1 && grid[c + 6] == 0) pos = c + 6;
        }
        if (pos == -1 && grid[0] == 2 && grid[4] == 2 && grid[8] == 0) pos = 8;
        if (pos == -1 && grid[0] == 2 && grid[4] == 0 && grid[8] == 2) pos = 4;
        if (pos == -1 && grid[0] == 0 && grid[4] == 2 && grid[8] == 2) pos = 0;
        if (pos == -1 && grid[2] == 2 && grid[4] == 2 && grid[6] == 0) pos = 6;
        if (pos == -1 && grid[2] == 2 && grid[4] == 0 && grid[6] == 2) pos = 4;
        if (pos == -1 && grid[2] == 0 && grid[4] == 2 && grid[6] == 2) pos = 2;

        if (pos == -1 && grid[0] == 1 && grid[4] == 1 && grid[8] == 0) pos = 8;
        if (pos == -1 && grid[0] == 1 && grid[4] == 0 && grid[8] == 1) pos = 4;
        if (pos == -1 && grid[0] == 0 && grid[4] == 1 && grid[8] == 1) pos = 0;
        if (pos == -1 && grid[2] == 1 && grid[4] == 1 && grid[6] == 0) pos = 6;
        if (pos == -1 && grid[2] == 1 && grid[4] == 0 && grid[6] == 1) pos = 4;
        if (pos == -1 && grid[2] == 0 && grid[4] == 1 && grid[6] == 1) pos = 2;

        if (pos == -1)
        { // Pick a random position
            int times = 0;
            Random rand = new();
            while (times < 100)
            { // Just to avoid problems
                times++;
                pos = rand.Next(0, 9);
                if (grid[pos] == 0) break;
            }
        }
        grid[pos] = 2;
    }
}
