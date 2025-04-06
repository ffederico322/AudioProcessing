using AudioProcessing.Receiver.Utils;
using System.IO.MemoryMappedFiles;
using AudioProcessing.Domain.Entity;

namespace AudioProcessing.Receiver.SharedMemory
{
    public class SharedMemoryClient : IDisposable
    {
        private readonly ConsoleLogger _logger;
        private readonly MemoryMappedFile _inputBuffer;
        private readonly MemoryMappedFile _outputBuffer;
        private readonly EventWaitHandle _inputEvent;
        private readonly EventWaitHandle _outputEvent;
        private readonly MessageSerializer _serializer;
        private bool _disposed;

        public SharedMemoryClient(string inputBufferName, string outputBufferName, ConsoleLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serializer = new MessageSerializer();

            // Вынести в отдельный метод инициалзилации
            try
            {
                // Подключаемся к буферам, созданным сервером
                _inputBuffer = MemoryMappedFile.OpenExisting(inputBufferName);
                _outputBuffer = MemoryMappedFile.OpenExisting(outputBufferName);

                // Подключаемся к событиям
                _inputEvent = EventWaitHandle.OpenExisting($"{inputBufferName}_event");
                _outputEvent = EventWaitHandle.OpenExisting($"{outputBufferName}_event");

                _logger.LogInformation($"Успешно подключен к буферам разделяемой памяти: {inputBufferName}, {outputBufferName}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при подключении к буферам разделяемой памяти: {ex.Message}");
                
                // Освобождаем ресурсы, которые могли быть созданы
                _inputBuffer?.Dispose();
                _outputBuffer?.Dispose();
                _inputEvent?.Dispose();
                _outputEvent?.Dispose();
                
                throw;
            }
        }

        public async Task<AudioMessage?> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Ожидаем сигнала о наличии данных
                await Task.Run(() => WaitForEvent(_inputEvent, cancellationToken), cancellationToken);

                // Читаем данные из буфера
                using var accessor = _inputBuffer.CreateViewAccessor();
                
                // Сначала читаем размер данных
                int size = accessor.ReadInt32(0);
                
                // Проверяем корректность размера
                if (size <= 0)
                {
                    _logger.LogWarning("Получен некорректный размер данных: {0}", size);
                    return null;
                }
                
                // Читаем данные
                byte[] data = new byte[size];
                accessor.ReadArray(sizeof(int), data, 0, size);

                // Десериализуем сообщение
                var message = _serializer.Deserialize(data);
                return message;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при получении сообщения: {ex.Message}");
                return null;
            }
        }

        public void SendMessage(AudioMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            try
            {
                // Сериализуем сообщение
                byte[] data = _serializer.Serialize(message);

                // Записываем данные в буфер
                using var accessor = _outputBuffer.CreateViewAccessor();

                // Проверяем, что данные помещаются в буфер
                if (data.Length + sizeof(int) > accessor.Capacity)
                {
                    throw new InvalidOperationException(
                        $"Размер данных ({data.Length} байт) превышает емкость буфера ({accessor.Capacity} байт)");
                }

                // Сначала записываем размер данных
                accessor.Write(0, data.Length);

                // Затем записываем сами данные
                accessor.WriteArray(sizeof(int), data, 0, data.Length);

                // Сигнализируем о наличии данных
                _outputEvent.Set();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при отправке сообщения: {ex.Message}");
                throw;
            }
        }

        private void WaitForEvent(EventWaitHandle eventHandle, CancellationToken cancellationToken)
        {
            WaitHandle[] waitHandles = { eventHandle, cancellationToken.WaitHandle };
            
            // Ожидаем сигнала события или отмены
            int index = WaitHandle.WaitAny(waitHandles);
            
            if (index == 1) // Индекс токена отмены
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Освобождаем управляемые ресурсы
                _inputBuffer?.Dispose();
                _outputBuffer?.Dispose();
                _inputEvent?.Dispose();
                _outputEvent?.Dispose();
            }

            _disposed = true;
        }

        ~SharedMemoryClient()
        {
            Dispose(false);
        }
    }
}