using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.Events;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Network.Simulation;
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
        
        // If no modspot lobbies exist, we need to make sure one is created
        bool createIfNoneExists = false;
        if (attributes.TryGetValue("SERVER_TYPE", out string? type))
            createIfNoneExists = type == "kartPark";
        
        List<GameBrowserGame> games = Bombd.RoomManager.SearchRooms(attributes, context.Connection.Platform, 1, createIfNoneExists);
        return CreateServerGameList(games);
    }

    [Transaction("subscribeGameEvents")]
    public ServerGameList? SubscribeGameEvents(TransactionContext context)
    {
        GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(context.Connection.UserId);
        if (player == null)
        {
            context.Response.Error = "notInGame";
            return null;
        }

        // The game provides a list of search attributes, but we only really want KART_PARK_HOME
        var attributes = NetworkReader.Deserialize<GameSearchAttributes>(context.Request["searchAttribs"]);
        if (!attributes.TryGetValue("KART_PARK_HOME", out string? kartPark))
        {
            context.Response.Error = "invalidSearchAttribs";
            return null;
        }
        
        // We should only be subscribing to events in the kart park that we're currently in
        if (kartPark != player.Room.Game.GameName)
        {
            context.Response.Error = "notInKartPark";
            return null;
        }
        
        player.ListeningForGameEvents = true;
        List<GameBrowserGame> games = [];
        // List<GameBrowserGame> games = Bombd.RoomManager.GetKartParkSubMatches(kartPark, context.Connection.Platform);
        return CreateServerGameList(games);
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
        
        // If you're not in a group, FreeSlotsRequired will be 0
        int freeSlotsRequired = Math.Max(1, searchData.FreeSlotsRequired);
        
        List<GameBrowserGame> games = Bombd.RoomManager.SearchRooms(searchData.Attributes, context.Connection.Platform, freeSlotsRequired, false);
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
        // var request = NetcodeTransaction.MakeRequest(Name, "gameEvent");
        // request["eventType"] = "playerJoined";
        // request["gameinfo"] = Convert.ToBase64String(NetworkWriter.Serialize(args.Room.GetGameBrowserInfo()));
        // foreach (GamePlayer player in args.Room.Game.Players)
        // {
        //     if (!player.ListeningForGameEvents) continue;
        //     SendTransaction(player.UserId, request);
        // }
    }

    private void OnPlayerLeft(object? sender, PlayerLeaveEventArgs args)
    {
        // var request = NetcodeTransaction.MakeRequest(Name, "gameEvent");
        // request["eventType"] = "playerLeft";
        // request["gameinfo"] = Convert.ToBase64String(NetworkWriter.Serialize(args.Room.GetGameBrowserInfo()));
        // foreach (GamePlayer player in args.Room.Game.Players)
        // {
        //     if (!player.ListeningForGameEvents) continue;
        //     SendTransaction(player.UserId, request);
        // }
    }
}