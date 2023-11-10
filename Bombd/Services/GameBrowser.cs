using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Simulation;
using Bombd.Types.Events;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Requests;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("gamebrowser", 10412, ProtocolType.TCP)]
public class GameBrowser : BombdService
{
    public GameBrowser()
    {
        Bombd.GameServer.OnPlayerJoined += OnPlayerJoin;
        Bombd.GameServer.OnPlayerLeft += OnPlayerLeft;
    }
    
    [Transaction("listGames")]
    public ServerGameList ListGames(TransactionContext context)
    {
        var attributes = NetworkReader.Deserialize<GameAttributes>(context.Request["attributes"]);
        List<GameBrowserGame> games = Bombd.RoomManager.SearchRooms(attributes, context.Connection.Platform);
        return CreateServerGameList(games);
    }

    [Transaction("subscribeGameEvents")]
    public ServerGameList? SubscribeGameEvents(TransactionContext context)
    {
        GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(context.Connection.UserId);
        if (player == null)
        {
            context.Response.Error = "NotInKartPark";
            return null;
        }
        
        player.ListeningForGameEvents = true;
        return CreateServerGameList(new List<GameBrowserGame> { player.Room.GetGameBrowserInfo() });
    }
    
    [Transaction("unSubscribeGameEvents")]
    public void UnsubscribeGameEvents(TransactionContext context)
    {
        GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(context.Connection.UserId);
        if (player == null) return;
        player.ListeningForGameEvents = false;
    }
    
    [Transaction("RequestGlobalPlayerCount")]
    public void RequestGlobalPlayerCount(TransactionContext context)
    {
        context.Response["GlobalPlayerCount"] = UserInfo.Count.ToString();
    }

    [Transaction("requestPlayerCount")]
    public void RequestPlayerCount(TransactionContext context)
    {
        var request = NetworkReader.Deserialize<GamePlayerCounts>(context.Request["requestParams"]);
        Bombd.RoomManager.FillCreationPlayerCounts(request);
        context.Response["requestParams"] = Convert.ToBase64String(NetworkWriter.Serialize(request));
    }

    [Transaction("requestBusiestCount")]
    public void RequestBusiestCount(TransactionContext context)
    {
        var creations = Bombd.RoomManager.GetBusiestCreations();
        context.Response["BusiestGames"] = Convert.ToBase64String(NetworkWriter.Serialize(creations));
    }

    [Transaction("listGamesMatchmaking")]
    public ServerGameList ListGamesMatchmaking(TransactionContext context)
    {
        // TODO: Search based on all parameters, not just the game attributes.
        var searchData = NetworkReader.Deserialize<GameSearchData>(context.Request["searchData"]);
        List<GameBrowserGame> games = Bombd.RoomManager.SearchRooms(searchData.Attributes, context.Connection.Platform, false);
        return CreateServerGameList(games);
    }
    
    private ServerGameList CreateServerGameList(List<GameBrowserGame> games)
    {
        int timeOfDeath = TimeHelper.LocalTime + 60 * 60 * 1000;
        var gameManager = Bombd.GetService<GameManager>();
        
        return new ServerGameList
        {
            Header = new GameListHeader
            {
                ClusterUuid = Bombd.ClusterUuid,
                GameManagerIp = BombdConfig.Instance.ExternalIP,
                GameManagerPort = gameManager.Port.ToString(),
                GameManagerUuid = gameManager.Uuid,
                NumGamesInList = games.Count,
                TimeOfDeath = timeOfDeath
            },
            Games = games,
            TimeOfDeath = timeOfDeath
        };
    }
    
    private void OnPlayerJoin(object? sender, PlayerJoinEventArgs args)
    {
        var request = NetcodeTransaction.MakeRequest(Name, "gameEvent");
        request["eventType"] = "playerJoined";
        request["gameinfo"] = Convert.ToBase64String(NetworkWriter.Serialize(args.Room.GetGameBrowserInfo()));
        foreach (GamePlayer player in args.Room.Game.Players)
        {
            if (!player.ListeningForGameEvents) continue;
            SendTransactionToUser(player.UserId, request);
        }
    }

    private void OnPlayerLeft(object? sender, PlayerLeaveEventArgs args)
    {
        var request = NetcodeTransaction.MakeRequest(Name, "gameEvent");
        request["eventType"] = "playerLeft";
        request["gameinfo"] = Convert.ToBase64String(NetworkWriter.Serialize(args.Room.GetGameBrowserInfo()));
        foreach (GamePlayer player in args.Room.Game.Players)
        {
            if (!player.ListeningForGameEvents) continue;
            SendTransactionToUser(player.UserId, request);
        }
    }
}