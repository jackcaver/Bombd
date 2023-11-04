using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Serialization.Wrappers;
using Bombd.Simulation;
using Bombd.Types.Events;
using Bombd.Types.GameBrowser;
using Bombd.Types.GameManager;
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
        // Check if the user reserved a slot in this game
        int reservationKey = 0;
        if (context.Request.TryGet("reservationKey", out string param))
        {
            if (!int.TryParse(param, out reservationKey))
            {
                context.Response.Error = "ParseFail";
                return;
            }
        }
        context.Request.TryGet("guest", out string guest);
        
        context.Response["listenIP"] = BombdConfig.Instance.ExternalIP;
        context.Response["listenPort"] = Bombd.GameServer.Port.ToString();
        context.Response["hashSalt"] = CryptoHelper.Salt.ToString();
        context.Response["sessionId"] = context.Connection.SessionId.ToString();

        Bombd.GameServer.AddPlayerToJoinQueue(new PlayerJoinRequest
        {
            Timestamp = TimeHelper.LocalTime,
            UserId = context.Connection.UserId,
            GameName = context.Request["gamename"],
            ReservationKey = reservationKey,
            Guest = guest
        });
    }

    [Transaction("hostGame")]
    public void HostGame(TransactionContext context)
    {
        GameRoom room = Bombd.RoomManager.CreateRoom(new CreateGameRequest
        {
            Platform = context.Connection.Platform,
            Attributes = NetworkReader.Deserialize<GameAttributes>(context.Request["attributes"]),
            OwnerUserId = context.Connection.UserId
        });
        
        context.Response["gamename"] = room.Game.GameName;
        context.Response["listenIP"] = BombdConfig.Instance.ExternalIP;
        context.Response["listenPort"] = Bombd.GameServer.Port.ToString();
        context.Response["hashSalt"] = CryptoHelper.Salt.ToString();
        context.Response["sessionId"] = context.Connection.SessionId.ToString();
        
        context.Request.TryGet("guest", out string guest);
        Bombd.GameServer.AddPlayerToJoinQueue(new PlayerJoinRequest
        {
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
        if (!int.TryParse(context.Request["numSlots"], out int numGuests))
        {
            context.Response.Error = "ParseFail";
            return;
        }
        
        if (Bombd.GameServer.ReserveSlotInGame(gameName, out int reservationKey))
        {
            context.Response["reservationKey"] = reservationKey.ToString();
        }
        else context.Response.Error = "RoomFull";
    }

    [Transaction("logClientMessage")]
    public void LogClientMessage(TransactionContext context)
    {
    }

    [Transaction("leaveCurrentGame")]
    public void LeaveCurrentGame(TransactionContext context)
    {
        Bombd.GameServer.AddPlayerToLeaveQueue(context.Connection.UserId, context.Connection.Username,
            "Player left game");
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

        Bombd.GameServer.AddMigrationGroup(new GameMigrationRequest
        {
            HostUserId = context.Connection.UserId,
            Platform = context.Connection.Platform,
            GameName = gameName,
            Attributes = attributes,
            PlayerIdList = players
        });
    }
    
    private void OnPlayerJoin(object? sender, PlayerJoinEventArgs args)
    {
        GamePlayer newPlayer = args.Player;
        GameManagerGame game = args.Room.Game;

        string requestName = args.WasMigration ? "gameMigrationSuccess" : "joinGameCompleted";
        SendTransactionToUser(newPlayer.UserId, requestName, game);

        var request = NetcodeTransaction.MakeRequest(Name, "playerJoined", new PlayerJoinInfo
        {
            PlayerName = newPlayer.Username,
            PlayerId = newPlayer.PlayerId,
            UserId = newPlayer.UserId,
            GuestCount = newPlayer.Guests.Count
        });

        foreach (GamePlayer player in args.Room.Game.Players)
        {
            if (player.UserId == newPlayer.UserId) continue;
            SendTransactionToUser(player.UserId, request);
        }
    }

    private void OnPlayerLeft(object? sender, PlayerLeaveEventArgs args)
    {
        var request = NetcodeTransaction.MakeRequest(Name, "playerLeft", new PlayerLeaveInfo
        {
            PlayerName = args.PlayerName,
            Reason = args.Reason
        });

        foreach (GamePlayer player in args.Room.Game.Players) SendTransactionToUser(player.UserId, request);
    }
}