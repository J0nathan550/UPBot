using Discord.Interactions;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UPBot.UPBot_Code;

/// <summary>
/// This command implements a Logs command.
/// It can be used by admins to check the logs and download them
/// author: CPU
/// </summary>
[Group("logs", "Commands to show the logs")]
public class SlashLogs : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("show", "Allows to see and download guild logs")]
    public async Task LogsCommand([Summary("numeroflines", "How many lines of logs to get")][MinValue(5)][MaxValue(25)] long numLines)
    {
        if (Context.Guild == null) return;
        Utils.LogUserCommand(Context);

        string logs = Utils.GetLogsPath(Context.Guild.Name);
        if (logs == null)
        {
            await RespondAsync($"There are no logs today for the guild **{Context.Guild.Name}**", ephemeral: true);
            return;
        }

        List<string> lines = [];
        await using (var fs = new FileStream(logs, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using var sr = new StreamReader(fs);
            while (!sr.EndOfStream)
            {
                lines.Add(await sr.ReadLineAsync());
            }
        }

        int start = lines.Count - (int)numLines;
        if (start < 0) start = 0;
        string res = "";
        while (start < lines.Count)
        {
            res += lines[start].Replace("```", "\\`\\`\\`") + "\n";
            start++;
        }
        if (res.Length > 1990) res = res[-1990..] + "...\n";
        res = $"Last {numLines} lines of logs:\n```\n" + res + "```";
        await RespondAsync(res);
    }


    [SlashCommand("save", "Creates a zip file of the last logs of the server")]
    public async Task LogsSaveCommand()
    {
        if (Context.Guild == null) return;
        Utils.LogUserCommand(Context);

        string logs = Utils.GetLogsPath(Context.Guild.Name);
        if (logs == null)
        {
            await RespondAsync($"There are no logs today for the guild **{Context.Guild.Name}**", ephemeral: true);
            return;
        }
        string logsFolder = Utils.GetLastLogsFolder(Context.Guild.Name, logs);
        string outfile = logsFolder[..^1] + ".zip";
        ZipFile.CreateFromDirectory(logsFolder, outfile);


        await using (FileStream fs = new(outfile, FileMode.Open, FileAccess.Read))
            await RespondWithFileAsync(fs, Path.GetFileName(outfile), "Zipped log in attachment");
        await Utils.DeleteFileDelayed(30, outfile);
        await Utils.DeleteFolderDelayed(30, logsFolder);
    }

    [SlashCommand("saveall", "Creates a zip file of the all the server logs")]
    public async Task LogsSaveAllCommand()
    {
        if (Context.Guild == null) return;
        Utils.LogUserCommand(Context);

        string logsFolder = Utils.GetAllLogsFolder(Context.Guild.Name);

        string outfile = logsFolder[..^1] + ".zip";
        ZipFile.CreateFromDirectory(logsFolder, outfile);

        await using (FileStream fs = new(outfile, FileMode.Open, FileAccess.Read))
            await RespondWithFileAsync(fs, Path.GetFileName(outfile), "Zipped logs in attachment");
        await Utils.DeleteFileDelayed(30, outfile);
        await Utils.DeleteFolderDelayed(30, logsFolder);
    }

    [SlashCommand("delete", "Removes the server logs")]
    public async Task LogsDeleteCommand([Summary("guildname", "The name of the guild, case sensitive, to confirm the delete")] string guildname)
    {
        if (Context.Guild == null) return;
        Utils.LogUserCommand(Context);

        string logs = Utils.GetLogsPath(Context.Guild.Name);
        if (logs == null)
        {
            await RespondAsync($"There are no logs today for the guild **{Context.Guild.Name}**", ephemeral: true);
            return;
        }

        if (!guildname.Equals(Context.Guild.Name))
        {
            await RespondAsync("You have to specify the full guild name after 'delete' (_case sensitive_) to confirm the delete of the logs.", ephemeral: true);
            return;
        }

        int num = Utils.DeleteAllLogs(Context.Guild.Name);
        if (num == 1)
            await RespondAsync($"1 log file for guild **{Context.Guild.Name}** has been deleted");
        else
            await RespondAsync($"{num} log files for guild **{Context.Guild.Name}** have been deleted");
    }

}
