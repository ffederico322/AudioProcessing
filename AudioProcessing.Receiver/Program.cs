using AudioProcessing.Receiver.AudioProcessing;
using AudioProcessing.Receiver.SharedMemory;
using AudioProcessing.Receiver.Utils;
using AudioProcessing.Domain.Entity;
using AudioProcessing.Domain.Enum;

namespace AudioProcessing.Receiver
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Настройка обработчика логирования
            ConsoleLogger logger = new ConsoleLogger("AudioReceiver");
            
            try
            {
                // Обработка аргументов командной строки
                if (args.Length < 1)
                {
                    logger.LogError("Требуется идентификатор сессии в качестве аргумента командной строки");
                    Console.WriteLine("Использование: AudioProcessing.Receiver.exe <session_id>");
                    return 1;
                }

                string sessionId = args[0];
                logger.LogInformation($"Запуск аудио-приемника для сессии: {sessionId}");

                // Создаем токен отмены для корректного завершения работы
                using var cts = new CancellationTokenSource();

                // Настройка обработки сигналов для корректного завершения
                Console.CancelKeyPress += (sender, e) =>
                {
                    logger.LogInformation("Получен сигнал прерывания, завершение работы...");
                    e.Cancel = true;
                    cts.Cancel();
                };

                // Создаем клиент для работы с разделяемой памятью
                string clientToReceiverBuffer = $"{sessionId}_client_to_receiver";
                string receiverToClientBuffer = $"{sessionId}_receiver_to_client";

                using var sharedMemoryClient = new SharedMemoryClient(
                    clientToReceiverBuffer, 
                    receiverToClientBuffer, 
                    logger);

                // Создаем процессор аудио
                using var audioProcessor = new AudioProcessor(logger);

                // Запускаем основной цикл обработки
                await RunProcessingLoopAsync(
                    sessionId, 
                    sharedMemoryClient, 
                    audioProcessor, 
                    logger, 
                    cts.Token);

                logger.LogInformation("Приемник аудио завершил работу");
                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Операция отменена, приемник аудио завершает работу");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError($"Необработанное исключение: {ex.Message}");
                logger.LogError(ex.StackTrace);
                return -1;
            }
        }

        private static async Task RunProcessingLoopAsync(
            string sessionId,
            SharedMemoryClient sharedMemoryClient,
            AudioProcessor audioProcessor,
            ConsoleLogger logger,
            CancellationToken cancellationToken)
        {
            // Отправляем сообщение об активации
            sharedMemoryClient.SendMessage(
                AudioMessage.CreateActivate(sessionId), 
                cancellationToken);

            logger.LogInformation("Приемник активирован, начинаем обработку аудио");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Ожидаем сообщение от клиента
                    var message = await sharedMemoryClient.ReceiveMessageAsync(cancellationToken);

                    if (message == null)
                    {
                        logger.LogWarning("Получено null-сообщение, пропускаем");
                        continue;
                    }

                    logger.LogDebug($"Получено сообщение типа {message.Type}, размер данных: {message.Data?.Length ?? 0} байт");

                    switch (message.Type)
                    {
                        case MessageType.AudioData:
                            if (message.Data != null && message.Data.Length > 0)
                            {
                                // Обрабатываем аудио-данные
                                byte[] processedData = audioProcessor.ProcessAudio(message.Data);

                                // Отправляем обработанные данные обратно
                                sharedMemoryClient.SendMessage(
                                    AudioMessage.CreateAudioData(sessionId, processedData), 
                                    cancellationToken);
                            }
                            else
                            {
                                logger.LogWarning("Получено сообщение с аудио-данными нулевой длины");
                            }
                            break;

                        case MessageType.Stop:
                            logger.LogInformation("Получена команда остановки");
                            return;

                        case MessageType.Config:
                            // Обработка конфигурации, если это необходимо
                            logger.LogInformation("Получены данные конфигурации");
                            break;

                        case MessageType.Heartbeat:
                            // Отвечаем на проверку соединения
                            sharedMemoryClient.SendMessage(
                                AudioMessage.CreateActivate(sessionId), // Используем Activate как Heartbeat ответ
                                cancellationToken);
                            break;

                        default:
                            logger.LogWarning($"Получено сообщение неизвестного типа: {message.Type}");
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Ошибка в цикле обработки: {ex.Message}");

                    // Отправляем сообщение об ошибке
                    try
                    {
                        sharedMemoryClient.SendMessage(
                            AudioMessage.CreateError(sessionId, ex.Message), 
                            cancellationToken);
                    }
                    catch
                    {
                        // Игнорируем ошибки при отправке сообщения об ошибке
                    }
                }
            }
        }
    }
}