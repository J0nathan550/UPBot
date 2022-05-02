﻿using System.Threading.Tasks;
using DSharpPlus.SlashCommands;

/// <summary>
/// This command implements a Version command.
/// Just to check the version of the bot
/// author: CPU
/// </summary>
/// 

public class SlashVersion : ApplicationCommandModule {

  [SlashCommand("version", "Get my version information")]
  public async Task VInfoCommand(InteractionContext ctx) {
    string authors = "**CPU**, **Duck**, **Eremiell**, **SlicEnDicE**, **J0nathan**, **Revolution**";

    await ctx.CreateResponseAsync(Utils.BuildEmbed("United Programming Bot", "**Version**: " + Utils.GetVersion() + "\n\nContributors: " +
      authors + "\n\nCode available on https://github.com/United-Programming/UPBot/", Utils.Yellow).Build());
  }

}


