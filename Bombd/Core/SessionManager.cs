using System.Collections.Concurrent;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Types.Gateway.Events;
using Bombd.Types.Services;

namespace Bombd.Core;

public class SessionManager
{
    private readonly object _sessionLock = new();
    private readonly Dictionary<int, Session> _sessions = new();
    
    public Session? Get(ConnectionBase connection)
    {
        lock (_sessionLock)
        {
            return _sessions.GetValueOrDefault(connection.SessionId);
        }
    }
    
    public static int GetSessionKey(string sessionUuid)
    {
        if (!int.TryParse(sessionUuid, out int sessionKey))
            sessionKey = CryptoHelper.StringHash32(sessionUuid);
        return sessionKey;
    }

    public void Clear()
    {
        lock (_sessionLock)
        {
            _sessions.Clear();
        }
    }
    
    public void Register(PlayerSessionCreatedEvent evt)
    {
        lock (_sessionLock)
        {
            Logger.LogInfo<SessionManager>($"Syncing session from Web API for {evt.Username}");
            int sessionId = GetSessionKey(evt.SessionUuid);
            _sessions[sessionId] = new Session
            {
                PlayerConnectUuid = evt.SessionUuid,
                PlayerConnectId = evt.PlayerConnectId,
                Username = evt.Username,
                Issuer = evt.Issuer,
                HashSalt = (uint)CryptoHelper.GetRandomSecret()
            };
        }
    }
    
    public void Unregister(PlayerSessionDestroyedEvent evt)
    {
        lock (_sessionLock)
        {
            int sessionId = GetSessionKey(evt.SessionUuid);
            if (_sessions.Remove(sessionId, out Session? session))
            {
                Logger.LogInfo<SessionManager>($"Destroying session for {session.Username}");
            }
        }
    }
}