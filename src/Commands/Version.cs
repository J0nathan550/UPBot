using Discord.Interactions;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

/// <summary>
/// This command implements a Version command.
/// Just to check the version of the bot
/// author: CPU
/// </summary>
/// 

public class SlashVersion : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("version", "Get my version information")]
    public async Task VInfoCommand()
    {
        string authors = "**CPU**, **J0nathan550**, **Eremiell**, **Duck**, **SlicEnDicE**, **Apoorv**, **Revolution**";

        await RespondAsync(embed: Utils.BuildEmbed("United Programming Bot",
          $"**Version**: {Utils.GetVersion()}\n\nContributors: {authors}\n\nCode available on https://github.com/United-Programming/UPBot/\n\nJoin United Programming discord: https://discord.gg/unitedprogramming",
          Utils.Yellow).Build());
    }

}
