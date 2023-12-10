using Bombd.Helpers;
using Bombd.Types.GameBrowser;

namespace Bombd.Types.Matchmaking;

public class MatchmakingPlayer
{
    public MatchmakingPlayer(int userId, Platform platform)
    {
        UserId = userId;
        Platform = platform;
    }
        
    public int StartTime;
    public int UserId;
    public Platform Platform;
    public List<GameAttributePair> SimpleFilters = new();
    public List<GameAttributePair> AdvancedFilters = new();
    public string MatchSizeTable;
    public int GroupSize;
    public int GuestCount;
}