using System.Collections.Concurrent;
using AudioProcessing.Application.Interfaces;
using AudioProcessing.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AudioProcessing.Application.Services
{
    public class AudioSessionService : IAudioSessionService
    {
        private readonly IProcessManager _processManager;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<AudioSessionService> _logger;
        private readonly string _receiverExecutablePath;
        private readonly ConcurrentDictionary<string, int> _sessionProcessIds = new();

        public AudioSessionService(
            IProcessManager processManager,
            ISessionManager sessionManager,
            ILogger<AudioSessionService> logger)
        {
            _processManager = processManager;
            _sessionManager = sessionManager;
            _logger = logger;
            
            _receiverExecutablePath = "AudioProcessing.Receiver.exe";
        }

        public async Task<string> CreateAudioSessionAsync()
        {
            // Создаем новую сессию

            // Обработать через try catch
            var sessionId = await _sessionManager.CreateSessionAsync();
            _logger.LogInformation("Создана новая аудио-сессия: {SessionId}", sessionId);

            return sessionId;
        }

        public async Task CloseAudioSessionAsync(string sessionId)
        {
            try
            {
                // Если для сессии запущен процесс, останавливаем его
                if (_sessionProcessIds.TryRemove(sessionId, out int processId))
                {
                    await _processManager.StopProcessAsync(processId);
                    _logger.LogInformation("Остановлен процесс приемника для сессии {SessionId}", sessionId);
                }

                // Удаляем информацию о сессии
                await _sessionManager.RemoveSessionAsync(sessionId);
                _logger.LogInformation("Закрыта аудио-сессия: {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при закрытии аудио-сессии {SessionId}", sessionId);

                // Вызвать свой custom exception
                throw;
            }
        }

        public async Task ActivateReceiverAsync(string sessionId)
        {
            try
            {
                // Запускаем процесс приемника для сессии
                string arguments = sessionId;
                int processId = await _processManager.StartProcessAsync(_receiverExecutablePath, arguments);
                
                // Сохраняем идентификатор процесса
                _sessionProcessIds[sessionId] = processId;
                
                // Активируем сессию
                await _sessionManager.ActivateReceiverAsync(sessionId);
                
                _logger.LogInformation("Активирован приемник для сессии {SessionId}, PID: {ProcessId}", 
                    sessionId, processId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при активации приемника для сессии {SessionId}", sessionId);
                throw;
            }
        }

        public async Task SendAudioToReceiverAsync(string sessionId, byte[] audioData)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Идентификатор сессии не может быть пустым", nameof(sessionId));
            
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Аудио-данные не могут быть пустыми", nameof(audioData));
            
            // Проверяем, что процесс приемника запущен
            if (_sessionProcessIds.TryGetValue(sessionId, out int processId) && 
                _processManager.IsProcessRunning(processId))
            {
                // Отправка данных будет происходить через SharedMemory в WebSocketHandler,
                // поэтому здесь нет необходимости в дополнительной логике
                return;
            }
            
            _logger.LogWarning("Не удалось отправить аудио данные: приемник для сессии {SessionId} не активен", 
                sessionId);
        }
    }
}