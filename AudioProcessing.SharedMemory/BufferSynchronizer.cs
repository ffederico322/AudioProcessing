namespace AudioProcessing.SharedMemory;

/// <summary>
/// Класс для синхронизации доступа к буферам разделяемой памяти
/// </summary>
public class BufferSynchronizer : IBufferSynchronizer, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
    private readonly ConcurrentDictionary<string, EventWaitHandle> _events = new();
    private readonly ILogger<BufferSynchronizer> _logger;
    private bool _disposed;

    /// <summary>
    /// Создает новый экземпляр класса BufferSynchronizer
    /// </summary>
    /// <param name="logger">Логгер для записи диагностической информации</param>
    public BufferSynchronizer(ILogger<BufferSynchronizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Асинхронно получает блокировку для доступа к буферу
    /// </summary>
    /// <param name="bufferName">Имя буфера</param>
    /// <param name="timeout">Максимальное время ожидания блокировки</param>
    /// <returns>Объект для освобождения блокировки через using</returns>
    public async Task<IDisposable> AcquireLockAsync(string bufferName, TimeSpan timeout = default)
    {
        if (string.IsNullOrEmpty(bufferName))
        {
            throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
        }

        // Если таймаут не задан, используем значение по умолчанию (10 секунд)
        timeout = timeout == default ? TimeSpan.FromSeconds(10) : timeout;

        try
        {
            // Получаем или создаем семафор для буфера
            var semaphore = _semaphores.GetOrAdd(bufferName, _ => new SemaphoreSlim(1, 1));

            // Пытаемся получить блокировку в течение указанного времени
            if (!await semaphore.WaitAsync(timeout))
            {
                throw new TimeoutException($"Истекло время ожидания блокировки для буфера {bufferName}");
            }

            // Возвращаем объект, который освободит блокировку при вызове Dispose
            return new SemaphoreLock(semaphore);
        }
        catch (Exception ex) when (!(ex is TimeoutException))
        {
            _logger.LogError(ex, "Ошибка при получении блокировки для буфера {BufferName}", bufferName);
            throw;
        }
    }

    /// <summary>
    /// Сигнализирует о наличии новых данных в буфере
    /// </summary>
    /// <param name="bufferName">Имя буфера</param>
    public void SignalDataAvailable(string bufferName)
    {
        if (string.IsNullOrEmpty(bufferName))
        {
            throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
        }

        try
        {
            // Получаем или создаем событие для буфера
            var eventHandle = _events.GetOrAdd(bufferName, _ => 
                new EventWaitHandle(false, EventResetMode.AutoReset, $"{bufferName}_event"));

            // Устанавливаем событие для уведомления о новых данных
            eventHandle.Set();
            
            _logger.LogTrace("Отправлен сигнал о наличии данных в буфере {BufferName}", bufferName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сигнализации о данных для буфера {BufferName}", bufferName);
            throw;
        }
    }

    /// <summary>
    /// Асинхронно ожидает появления новых данных в буфере
    /// </summary>
    /// <param name="bufferName">Имя буфера</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Задача, завершающаяся при появлении данных или отмене</returns>
    public async Task WaitForDataAsync(string bufferName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bufferName))
        {
            throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
        }

        try
        {
            // Получаем или создаем событие для буфера
            var eventHandle = _events.GetOrAdd(bufferName, _ => 
                new EventWaitHandle(false, EventResetMode.AutoReset, $"{bufferName}_event"));

            // Ожидаем сигнала о наличии новых данных
            await Task.Run(() => 
            {
                WaitHandle.WaitAny(new[] { eventHandle, cancellationToken.WaitHandle });
                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);
            
            _logger.LogTrace("Получен сигнал о наличии данных в буфере {BufferName}", bufferName);
        }
        catch (OperationCanceledException)
        {
            // Пробрасываем исключение при отмене операции
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при ожидании данных для буфера {BufferName}", bufferName);
            throw;
        }
    }

    /// <summary>
    /// Освобождает ресурсы
    /// </summary>
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
            // Освобождаем ресурсы семафоров
            foreach (var semaphore in _semaphores.Values)
            {
                semaphore.Dispose();
            }
            _semaphores.Clear();

            // Освобождаем ресурсы событий
            foreach (var eventHandle in _events.Values)
            {
                eventHandle.Dispose();
            }
            _events.Clear();
        }

        _disposed = true;
    }

    ~BufferSynchronizer()
    {
        Dispose(false);
    }

    /// <summary>
    /// Вспомогательный класс для управления блокировкой семафора
    /// </summary>
    private class SemaphoreLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public SemaphoreLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _semaphore.Release();
            _disposed = true;
        }
    }