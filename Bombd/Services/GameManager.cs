using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Serialization.Wrappers;
using Bombd.Types.Events;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
using Bombd.Types.Network.Room;
using Bombd.Types.Network.Simulation;
using Bombd.Types.Requests;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("gamemanager", 10505, ProtocolType.TCP)]
public class GameManager : BombdService
{
    public GameManager()
    {
        Bombd.GameServer.OnPlayerJoined += OnPlayerJoin;
        Bombd.GameServer.OnPlayerLeft += OnPlayerLeft;
    }
    
    [Transaction("joinGame")]
    public void JoinGame(TransactionContext context)
    {
        if (!context.Request.TryGet("gamename", out string? gameName))
        {
            context.Response.Error = JoinFailReason.GameNotFound;
            return;
        }
        
        GameRoom? room = Bombd.RoomManager.GetRoomByName(gameName);
        if (room == null || room.Platform != context.Connection.Platform)
        {
            context.Response.Error = JoinFailReason.GameNotFound;
            return;
        }

        int numSlots = 1;
        if (context.Request.TryGet("guest", out string guest))
        {
            // Only a single guest is allowed in online play
            if (guest.Split(",").Length > 1)
            {
                context.Response.Error = "tooManyGuests";
                return;
            }

            numSlots++;
        }
        
        if (!context.Request.TryGet("reservationKey", out string? reservationKey))
        {
            if (room.IsFull)
            {
                context.Response.Error = JoinFailReason.GameFull;
                return;
            }

            if (!Bombd.GameServer.ReserveSlotsInGame(gameName, numSlots, out reservationKey))
            {
                context.Response.Error = JoinFailReason.NotEnoughSlots;
                return;
            }
        }
        
        context.Response["listenIP"] = BombdConfig.Instance.ExternalIP;
        context.Response["listenPort"] = Bombd.GameServer.Port.ToString();
        context.Response["hashSalt"] = context.Session.HashSalt.ToString();
        context.Response["sessionId"] = context.Connection.SessionId.ToString();
        
        Bombd.GameServer.AddPlayerToJoinQueue(new PlayerJoinRequest
        {
            Username = context.Connection.Username,
            Timestamp = TimeHelper.LocalTime,
            UserId = context.Connection.UserId,
            GameName = gameName,
            ReservationKey = reservationKey,
            Guest = guest
        });
    }

    [Transaction("hostGame")]
    public void HostGame(TransactionContext context)
    {
        GameRoom room = Bombd.RoomManager.CreateRoom(new CreateGameRequest { 
            Platform = context.Connection.Platform, 
            Attributes = NetworkReader.Deserialize<GameAttributes>(context.Request["attributes"]), 
            OwnerUserId = context.Connection.UserId 
        });
        
        context.Response["gamename"] = room.Game.GameName;
        context.Response["listenIP"] = BombdConfig.Instance.ExternalIP;
        context.Response["listenPort"] = Bombd.GameServer.Port.ToString();
        context.Response["hashSalt"] = context.Session.HashSalt.ToString();
        context.Response["sessionId"] = context.Connection.SessionId.ToString();
        
        if (context.Request.TryGet("guest", out string guest))
        {
            // Only a single guest is allowed in online play
            if (guest.Split(",").Length > 1)
            {
                context.Response.Error = "tooManyGuests";
                return;
            }
        }
        
        Bombd.GameServer.AddPlayerToJoinQueue(new PlayerJoinRequest
        {
            Username = context.Connection.Username,
            Timestamp = TimeHelper.LocalTime,
            UserId = context.Connection.UserId,
            GameName = room.Game.GameName,
            Guest = guest
        });
    }

    [Transaction("reserveSlotsInGameForGroup")]
    public void ReserveSlotsInGameForGroup(TransactionContext context)
    {
        string gameName = context.Request["gamename"];
        if (!int.TryParse(context.Request["numSlots"], out int numSlots))
        {
            context.Response.Error = "ParseFail";
            return;
        }
        
        // NOTE:
        // If you're in a group, it'll just give 1 for numSlots?
        // If you're not in a group, it'll give 0?
        numSlots++;
        
        if (Bombd.GameServer.ReserveSlotsInGame(gameName, numSlots, out string? reservationKey))
        {
            context.Response["reservationKey"] = reservationKey;
        }
        else context.Response.Error = JoinFailReason.GameFull;
    }

    [Transaction("logClientMessage")]
    public void LogClientMessage(TransactionContext context)
    {
    }

    [Transaction("leaveCurrentGame")]
    public void LeaveCurrentGame(TransactionContext context)
    {
        GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(context.Connection.UserId);
        if (player == null) return;
        
        string currentGameName = player.Room.Game.GameName;
        if (!context.Request.TryGet("gamename", out string? gameName))
            gameName = currentGameName;
        
        // For the most part, leaveCurrentGame is basically pointless since the server will 
        // disconnect users from games if they drop the connection to the server.
        // But Karting will send leaveCurrentGame for the old pod gameroom *after* they
        // join another game, so just handle that case here by returning early.
        if (gameName != currentGameName) return;
        
        Bombd.GameServer.AddPlayerToLeaveQueue(context.Connection.UserId, context.Connection.Username, "userInitiatedLeave");
    }

    [Transaction("migrateToGame")]
    public void MigrateToGame(TransactionContext context)
    {
        context.Request.TryGet("gamename", out string gameName);
        if (!int.TryParse(context.Request["numPlayers"], out int numPlayers)) numPlayers = 0;
        List<GenericInt32> players =
            NetworkReader.Deserialize<GenericInt32>(context.Request["playerIdList"], numPlayers);

        GameAttributes? attributes = null;
        if (context.Request.Has("attributes"))
            attributes = NetworkReader.Deserialize<GameAttributes>(context.Request["attributes"]);
        
        context.Request.TryGet("guest", out string? guest);
        
        Bombd.GameServer.AddMigrationGroup(new GameMigrationRequest
        {
            HostUserId = context.Connection.UserId,
            Platform = context.Connection.Platform,
            GameName = gameName,
            Guest = guest,
            Attributes = attributes,
            PlayerIdList = players
        });
    }

    [Transaction("detachGuests")]
    [Transaction("attachGuests")]
    public void HandleGuestRequests(TransactionContext context)
    {
        GamePlayer? player = Bombd.RoomManager.GetPlayerInRoom(context.Connection.UserId);
        if (player == null)
        {
            context.Response.Error = "notInGame";
            return;
        }
        
        var block = NetworkReader.Deserialize<GuestStatusBlock>(context.Request["guestBlock"]);
        Bombd.GameServer.UpdateGuestStatuses(player, block);
        
        context.Response["guestBlock"] = Convert.ToBase64String(NetworkWriter.Serialize((INetworkWritable)block));
    }
    
    private void OnPlayerJoin(object? sender, PlayerJoinEventArgs args)
    {
        GamePlayer newPlayer = args.Player;
        GameManagerGame game = args.Room.Game;

        string requestName = args.WasMigration ? "gameMigrationSuccess" : "joinGameCompleted";
        SendTransaction(newPlayer.UserId, requestName, game);

        var request = NetcodeTransaction.MakeRequest(Name, "playerJoined", new PlayerJoinInfo
        {
            GameName = game.GameName,
            PlayerName = newPlayer.Username,
            PlayerId = newPlayer.PlayerId,
            UserId = newPlayer.UserId,
            GuestCount = newPlayer.Guests.Count
        });

        foreach (GamePlayer player in args.Room.Game.Players)
        {
            if (player.UserId == newPlayer.UserId) continue;
            SendTransaction(player.UserId, request);
        }
    }

    private void OnPlayerLeft(object? sender, PlayerLeaveEventArgs args)
    {
        var request = NetcodeTransaction.MakeRequest(Name, "playerLeft", new PlayerLeaveInfo
        {
            GameName = args.Room.Game.GameName,
            PlayerName = args.PlayerName,
            Reason = args.Reason
        });

        foreach (GamePlayer player in args.Room.Game.Players) SendTransaction(player.UserId, request);
    }
}