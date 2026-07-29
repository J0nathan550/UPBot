using Discord.WebSocket;

public class TrackChannel : Entity
{
    [Key] public ulong Guild;
    public ulong ChannelId;
    public bool trackJoin;
    public bool trackLeave;
    public bool trackRoles;

    [NotPersistent] public SocketTextChannel channel;



    public TrackChannel() { }

    public TrackChannel(ulong guild, ulong channel)
    {
        Guild = guild;
        ChannelId = channel;
    }

}