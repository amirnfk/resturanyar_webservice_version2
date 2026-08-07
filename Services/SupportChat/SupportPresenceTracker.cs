using System.Collections.Concurrent;

namespace resturanyar.Services.SupportChat
{
    /// <summary>
    /// Tracks admin SignalR connections that joined the presence group (Support Chat page open).
    /// </summary>
    public interface ISupportPresenceTracker
    {
        void AddPresence(string connectionId);
        void RemovePresence(string connectionId);
        bool IsSupportOnline { get; }
        int OnlineConnectionCount { get; }
    }

    public class SupportPresenceTracker : ISupportPresenceTracker
    {
        private readonly ConcurrentDictionary<string, byte> _presenceConnections = new();

        public void AddPresence(string connectionId)
        {
            if (!string.IsNullOrWhiteSpace(connectionId))
                _presenceConnections[connectionId] = 0;
        }

        public void RemovePresence(string connectionId)
        {
            if (!string.IsNullOrWhiteSpace(connectionId))
                _presenceConnections.TryRemove(connectionId, out _);
        }

        public bool IsSupportOnline => !_presenceConnections.IsEmpty;

        public int OnlineConnectionCount => _presenceConnections.Count;
    }
}
