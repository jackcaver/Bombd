using System.Collections.Concurrent;
using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.GameBrowser;
using Bombd.Types.Matchmaking;
using Bombd.Types.Requests;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("matchmaking", 10510, ProtocolType.TCP)]
public class Matchmaking : BombdService
{
    private readonly ConcurrentQueue<MatchmakingPlayer> _pendingStartRequests = new();
    private readonly ConcurrentQueue<MatchmakingCancelRequest> _pendingCancelRequests = new();
    private readonly List<MatchmakingPlayer> _matchmakingPlayers = new();
    
    [Transaction("beginMatchmaking")]
    public void BeginMatchmaking(TransactionContext context)
    {
        var player = new MatchmakingPlayer(context.Connection.UserId, context.Connection.Platform);
        
        int numSimpleFilters = int.Parse(context.Request["numSimpleFilters"]);
        if (numSimpleFilters != 0)
        {
            player.SimpleFilters =
                NetworkReader.Deserialize<GameAttributePair>(context.Request["simpleFilters"], numSimpleFilters);
        }

        int numAdvancedFilters = int.Parse(context.Request["numAdvancedFilters"]);
        if (numAdvancedFilters != 0)
        {
            player.AdvancedFilters =
                NetworkReader.Deserialize<GameAttributePair>(context.Request["advancedFilters"], numAdvancedFilters);
        }
        
        player.MatchSizeTable = context.Request["matchSizeTable"];
        player.GroupSize = int.Parse(context.Request["groupSize"]);
        player.GuestCount = int.Parse(context.Request["numGuests"]);
        
        _pendingStartRequests.Enqueue(player);
    }

    [Transaction("cancelMatchmaking")]
    public void CancelMatchmaking(TransactionContext context)
    {
        _pendingCancelRequests.Enqueue(new MatchmakingCancelRequest
        {
            UserId = context.Connection.UserId,
            WasRequested = true
        });
    }
    
    public override void OnTick()
    {
        // First process everybody who requested matchmaking start
        while (_pendingStartRequests.TryDequeue(out MatchmakingPlayer? joiningPlayer))
        {
            // If the player is already matchmaking, just ignore the request, this shouldn't normally happen.
            int index = _matchmakingPlayers.FindIndex(player => player.UserId == joiningPlayer.UserId);
            if (index != -1) continue;

            NetcodeTransaction transaction;
            // XP races aren't implemented in Modnation, so send back an error for now.
            if (joiningPlayer.Platform == Platform.ModNation)
            {
                transaction = NetcodeTransaction.MakeRequest(Name, "matchmakingError");
                transaction.Error = "noGamesAvailable";
                SendTransaction(joiningPlayer.UserId, transaction);
                continue;
            }
            
            // Tell the game that we've started matchmaking
            joiningPlayer.StartTime = TimeHelper.LocalTime;
            transaction = NetcodeTransaction.MakeRequest(Name, "matchmakingBegin");
            transaction["matchmakingBeginTime"] = joiningPlayer.StartTime.ToString();
            SendTransaction(joiningPlayer.UserId, transaction);
            _matchmakingPlayers.Add(joiningPlayer);
        }
        
        // Handle everybody who requested a cancel
        while (_pendingCancelRequests.TryDequeue(out MatchmakingCancelRequest leaveRequest))
        {
            int index = _matchmakingPlayers.FindIndex(player => player.UserId == leaveRequest.UserId);
            if (index == -1) continue;
            
            // If this cancel was requested rather than triggered by a disconnect.
            if (leaveRequest.WasRequested)
            {
                var transaction = NetcodeTransaction.MakeRequest(Name, "matchmakingCanceled");
                SendTransaction(leaveRequest.UserId, transaction);   
            }
            
            _matchmakingPlayers.RemoveAt(index);
        }
        
        // TODO: Implement actual matchmaking
        // For now, just give the game back an empty gameroom to join
        foreach (var player in _matchmakingPlayers)
        {
            var attributes = new GameAttributes();
            foreach (var filter in player.SimpleFilters)
                attributes[filter.Key] = filter.Value;
            foreach (var filter in player.AdvancedFilters)
                attributes[filter.Key] = filter.Value;
            
            var gamemanager = Bombd.GetService<GameManager>();
            var room = Bombd.RoomManager.CreateRoom(new CreateGameRequest
            {
                OwnerUserId = player.UserId,
                Platform = player.Platform,
                Attributes = attributes
            });
            
            var request = NetcodeTransaction.MakeRequest(Name, "requestJoinGame");
            request["gameName"] = room.Game.GameName;
            request["host_uuid"] = gamemanager.Uuid;
            request["host_ip"] = BombdConfig.Instance.ExternalIP;
            request["host_port"] = Bombd.GameServer.Port.ToString();
            request["host_cluster_uuid"] = Bombd.ClusterUuid;
            SendTransaction(player.UserId, request);
        }
        
        _matchmakingPlayers.Clear();
    }
    
    public override void OnDisconnected(ConnectionBase connection)
    {
        Bombd.SessionManager.UnregisterSession(connection);
        UserInfo.TryRemove(connection.UserId, out _);
        
        // In case we're currently matchmaking, need to make sure that it gets cleaned up.
        _pendingCancelRequests.Enqueue(new MatchmakingCancelRequest
        {
            UserId = connection.UserId,
            WasRequested = false
        });
        
        Logger.LogInfo<Matchmaking>($"{connection.Username} has been disconnected.");
    }
}