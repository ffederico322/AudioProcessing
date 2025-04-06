using System.Collections.Concurrent;

namespace AudioProcessing.Session
{
    public class SessionStorage
    {
        private readonly ConcurrentDictionary<string, bool> _sessions = new();

        public void AddSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            _sessions[sessionId] = false; // По умолчанию сессия неактивна
        }

        public void RemoveSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            _sessions.TryRemove(sessionId, out _);
        }

        public bool SessionExists(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            return _sessions.ContainsKey(sessionId);
        }

        public void SetSessionActive(string sessionId, bool isActive)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            if (!_sessions.ContainsKey(sessionId))
                throw new KeyNotFoundException($"Сессия не найдена: {sessionId}");

            _sessions[sessionId] = isActive;
        }

        public bool IsSessionActive(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            if (_sessions.TryGetValue(sessionId, out bool isActive))
            {
                return isActive;
            }

            throw new KeyNotFoundException($"Сессия не найдена: {sessionId}");
        }

        public List<string> GetActiveSessions()
        {
            return _sessions.Where(pair => pair.Value).Select(pair => pair.Key).ToList();
        }

        public List<string> GetAllSessions()
        {
            return _sessions.Keys.ToList();
        }
    }
}