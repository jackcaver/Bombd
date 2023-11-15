using System.Collections.Concurrent;
using Bombd.Helpers;
using Bombd.Logging;
using Bombd.Protocols;
using Bombd.Types.Services;

namespace Bombd.Core;

public class SessionManager
{
    private readonly object _sessionLock = new();
    private readonly ConcurrentDictionary<int, Session> _sessions = new();

    public Session GetSession(ConnectionBase connection) => _sessions[connection.SessionId];

    public void RegisterSession(ConnectionBase connection)
    {
        lock (_sessionLock)
        {
            int sessionId = connection.SessionId;
            if (!_sessions.TryGetValue(sessionId, out Session? session))
            {
                sessionId = CryptoHelper.GetRandomSecret();
                int hashSalt = CryptoHelper.GetRandomSecret();
                session = new Session
                {
                    GameName = string.Empty,
                    HashSalt = hashSalt
                };

                connection.SessionId = sessionId;
                _sessions[sessionId] = session;

                Logger.LogInfo<SessionManager>($"Registered new session for {connection.Username}.");
            }

            connection.HashSalt = session.HashSalt;
            if (connection.Service.Name != "directory")
            {
                session.ConnectedServices.Add(connection.Service.Name);
                Logger.LogInfo<SessionManager>(
                    $"Added {connection.Service.Name} to {connection.Username}'s registered services.");
            }    
        }
    }

    public void UnregisterSession(ConnectionBase connection)
    {
        lock (_sessionLock)
        {
            if (!_sessions.TryGetValue(connection.SessionId, out Session? session))
            {
                Logger.LogWarning<SessionManager>(
                    $"Tried to unregister session for {connection.Username}, but they didn't have one!");
                return;
            }

            session.ConnectedServices.Remove(connection.Service.Name);

            Logger.LogInfo<SessionManager>(
                $"Removed {connection.Service.Name} from {connection.Username}'s registered services.");

            if (session.ConnectedServices.Count == 0)
            {
                Logger.LogInfo<SessionManager>(
                    $"Destroying session for {connection.Username} since all services have been disconnected.");
                _sessions.TryRemove(connection.SessionId, out _);
            }   
        }
    }
}