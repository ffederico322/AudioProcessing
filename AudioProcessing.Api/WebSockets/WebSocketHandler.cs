using AudioProcessing.Domain.Entity;
using AudioProcessing.Domain.Enum;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessing.Application.Interfaces;
using AudioProcessing.Domain.Interfaces;

namespace AudioProcessing.Api.WebSockets
{
    public class WebSocketHandler
    {
        private readonly IAudioSessionService _sessionService;
        private readonly ISharedMemoryManager _memoryManager;
        private readonly ILogger<WebSocketHandler> _logger;
        private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionTokens = new();

        public WebSocketHandler(
            IAudioSessionService sessionService,
            ISharedMemoryManager memoryManager,
            ILogger<WebSocketHandler> logger)
        {
            _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task HandleWebSocketAsync(WebSocket webSocket, CancellationToken externalToken)
        {
            // Создаем новую сессию
            string sessionId = await _sessionService.CreateAudioSessionAsync();
            _logger.LogInformation("Создана новая аудио-сессия с идентификатором {SessionId}", sessionId);

            try
            {
                _sockets.TryAdd(sessionId, webSocket);
                var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                _sessionTokens.TryAdd(sessionId, sessionCts);

                // Регистрируем буферы для этой сессии
                RegisterSessionBuffers(sessionId);

                // Отправляем сообщение активации
                await _sessionService.ActivateReceiverAsync(sessionId);

                // Запускаем задачи для обработки входящих и исходящих сообщений
                var receiveFromClientTask = ReceiveFromClientAsync(sessionId, webSocket, sessionCts.Token);
                var receiveFromReceiverTask = ReceiveFromReceiverAsync(sessionId, webSocket, sessionCts.Token);

                // Ожидаем завершения любой из задач (например, при закрытии соединения)
                await Task.WhenAny(receiveFromClientTask, receiveFromReceiverTask);

                // Отменяем токен, чтобы остановить оставшуюся задачу
                if (!sessionCts.IsCancellationRequested)
                {
                    sessionCts.Cancel();
                }

                // Дожидаемся завершения всех задач
                await Task.WhenAll(
                    receiveFromClientTask.ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted),
                    receiveFromReceiverTask.ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке WebSocket-соединения для сессии {SessionId}", sessionId);
            }
            finally
            {
                // Освобождаем ресурсы и завершаем сессию
                await CleanupSessionAsync(sessionId);
            }
        }

        private async Task ReceiveFromClientAsync(string sessionId, WebSocket webSocket, CancellationToken token)
        {
            var buffer = new byte[4096];
            string clientToReceiverBuffer = GetClientToReceiverBufferName(sessionId);

            try
            {
                while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Клиент запросил закрытие WebSocket для сессии {SessionId}", sessionId);
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Закрытие соединения по запросу клиента",
                            CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _logger.LogDebug("Получены аудио-данные от клиента: {ByteCount} байт", result.Count);

                        // Создаем сообщение с аудио-данными
                        var audioMessage = AudioMessage.CreateAudioData(sessionId, buffer.AsSpan(0, result.Count).ToArray());
                        
                        // Сериализуем и записываем в разделяемую память
                        byte[] serializedMessage = audioMessage.Serialize();
                        _memoryManager.Write(clientToReceiverBuffer, serializedMessage, 0, serializedMessage.Length);
                    }
                    else
                    {
                        _logger.LogWarning("Получено сообщение неподдерживаемого типа: {MessageType}", result.MessageType);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение при отмене токена
                _logger.LogInformation("Задача приема данных от клиента отменена для сессии {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приеме данных от клиента для сессии {SessionId}", sessionId);
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.InternalServerError,
                    "Внутренняя ошибка сервера",
                    CancellationToken.None);
            }
        }

        private async Task ReceiveFromReceiverAsync(string sessionId, WebSocket webSocket, CancellationToken token)
        {
            string receiverToClientBuffer = GetReceiverToClientBufferName(sessionId);

            try
            {
                while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    // Ожидаем появления данных в разделяемой памяти
                    byte[] serializedMessage;
                    try
                    {
                        serializedMessage = await _memoryManager.WaitAndReadAsync(receiverToClientBuffer, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Выходим из цикла при отмене токена
                        break;
                    }

                    if (serializedMessage == null || serializedMessage.Length == 0)
                    {
                        continue;
                    }

                    // Десериализуем сообщение
                    var audioMessage = AudioMessage.Deserialize(serializedMessage);

                    switch (audioMessage.Type)
                    {
                        case MessageType.AudioData:
                            _logger.LogDebug("Получены обработанные аудио-данные от приемника: {ByteCount} байт", audioMessage.Data.Length);
                            
                            // Отправляем данные клиенту через WebSocket
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(audioMessage.Data),
                                WebSocketMessageType.Binary,
                                true,
                                token);
                            break;

                        case MessageType.Error:
                            _logger.LogWarning("Получено сообщение об ошибке от приемника: {ErrorMessage}", audioMessage.GetErrorMessage());
                            
                            // Отправляем сообщение об ошибке клиенту
                            var errorBytes = Encoding.UTF8.GetBytes(audioMessage.GetErrorMessage());
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(errorBytes),
                                WebSocketMessageType.Text,
                                true,
                                token);
                            break;

                        default:
                            _logger.LogDebug("Получено служебное сообщение от приемника типа {MessageType}", audioMessage.Type);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Ошибка при приеме данных от приемника для сессии {SessionId}", sessionId);
                    try
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.InternalServerError,
                            "Ошибка при обработке данных от приемника",
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Игнорируем ошибки при попытке закрыть соединение
                    }
                }
            }
        }

        private void RegisterSessionBuffers(string sessionId)
        {
            const int bufferSize = 1024 * 1024; // 1 МБ для каждого буфера
            
            // Буфер для передачи данных от клиента приемнику
            _memoryManager.RegisterBuffer(GetClientToReceiverBufferName(sessionId), bufferSize);
            
            // Буфер для передачи данных от приемника клиенту
            _memoryManager.RegisterBuffer(GetReceiverToClientBufferName(sessionId), bufferSize);
            
            _logger.LogDebug("Зарегистрированы буферы для сессии {SessionId}", sessionId);
        }

        private async Task CleanupSessionAsync(string sessionId)
        {
            try
            {
                // Отмечаем токен как отмененный
                if (_sessionTokens.TryRemove(sessionId, out var cts))
                {
                    if (!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                    cts.Dispose();
                }

                // Удаляем WebSocket из словаря
                _sockets.TryRemove(sessionId, out _);

                // Закрываем сессию и освобождаем ресурсы
                await _sessionService.CloseAudioSessionAsync(sessionId);

                // Освобождаем буферы разделяемой памяти
                _memoryManager.UnregisterBuffer(GetClientToReceiverBufferName(sessionId));
                _memoryManager.UnregisterBuffer(GetReceiverToClientBufferName(sessionId));

                _logger.LogInformation("Сессия {SessionId} успешно завершена и ресурсы освобождены", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке ресурсов для сессии {SessionId}", sessionId);
            }
        }

        private static string GetClientToReceiverBufferName(string sessionId) => $"{sessionId}_client_to_receiver";
        private static string GetReceiverToClientBufferName(string sessionId) => $"{sessionId}_receiver_to_client";
    }
}