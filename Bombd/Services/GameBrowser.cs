using Bombd.Attributes;
using Bombd.Core;
using Bombd.Helpers;
using Bombd.Protocols;
using Bombd.Serialization;
using Bombd.Types.GameBrowser;
using Bombd.Types.Services;

namespace Bombd.Services;

[Service("gamebrowser", 10412, ProtocolType.TCP)]
public class GameBrowser : BombdService
{
    [Transaction("listGames")]
    public ServerGameList ListGames(TransactionContext context)
    {
        int timeOfDeath = TimeHelper.LocalTime + 60 * 60 * 1000;
        var gameManager = Bombd.GetService<GameManager>();

        var attributes = NetworkReader.Deserialize<GameAttributes>(context.Request["attributes"]);
        attributes.TryAdd("COMM_CHECKSUM", ((int)context.Connection.Platform).ToString());

        List<GameBrowserGame> games = Bombd.RoomManager.SearchRooms(attributes, context.Connection.Platform);
        var gameList = new ServerGameList
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

        return gameList;
    }

    [Transaction("subscribeGameEvents")]
    public ServerGameList SubscribeGameEvents(TransactionContext context) => ListGames(context);

    [Transaction("unSubscribeGameEvents")]
    public void UnsubscribeGameEvents(TransactionContext context)
    {
    }

    [Transaction("RequestGlobalPlayerCount")]
    public void RequestGlobalPlayerCount(TransactionContext context)
    {
        context.Response["GlobalPlayerCount"] = UserInfo.Count.ToString();
    }
}