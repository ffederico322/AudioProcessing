using AudioProcessing.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AudioProcessing.Session
{
    public class SessionManager : ISessionManager
    {
        private readonly ILogger<SessionManager> _logger;
        private readonly SessionStorage _sessionStorage;

        public SessionManager(ILogger<SessionManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessionStorage = new SessionStorage();
        }

        public Task<string> CreateSessionAsync()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            _sessionStorage.AddSession(sessionId);
            
            _logger.LogInformation("Создана новая сессия: {SessionId}", sessionId);
            
            return Task.FromResult(sessionId);
        }

        public Task RemoveSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            _sessionStorage.RemoveSession(sessionId);
            _logger.LogInformation("Удалена сессия: {SessionId}", sessionId);
            
            return Task.CompletedTask;
        }

        public Task ActivateReceiverAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));

            if (!_sessionStorage.SessionExists(sessionId))
            {
                throw new InvalidOperationException($"Сессия не найдена: {sessionId}");
            }

            _sessionStorage.SetSessionActive(sessionId, true);
            _logger.LogInformation("Сессия активирована: {SessionId}", sessionId);
            
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> GetActiveSessionsAsync()
        {
            var activeSessions = _sessionStorage.GetActiveSessions();
            return Task.FromResult<IReadOnlyCollection<string>>(activeSessions);
        }
    }
}