using AudioProcessing.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessing.Infrastructure.SharedMemory
{
    public class SharedMemoryManager : ISharedMemoryManager, IDisposable
    {
        private readonly Dictionary<string, MemoryMappedFile> _buffers = new();
        private readonly Dictionary<string, EventWaitHandle> _events = new();
        private readonly ILogger<SharedMemoryManager> _logger;
        private bool _disposed;

        public SharedMemoryManager(ILogger<SharedMemoryManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterBuffer(string bufferName, int size)
        {
            if (string.IsNullOrEmpty(bufferName))
            {
                throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
            }

            if (size <= 0)
            {
                throw new ArgumentException("Размер буфера должен быть положительным числом", nameof(size));
            }

            try
            {
                // Создаем файл отображения в памяти
                // как написать код для всех ОС
                _buffers[bufferName] = MemoryMappedFile.CreateOrOpen(bufferName, size);
                
                // Создаем именованное событие для уведомлений
                _events[bufferName] = new EventWaitHandle(false, EventResetMode.AutoReset, $"{bufferName}_event");
                
                _logger.LogDebug("Зарегистрирован буфер {BufferName} размером {Size} байт", bufferName, size);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при регистрации буфера {BufferName}", bufferName);
                throw;
            }
        }

        public void UnregisterBuffer(string bufferName)
        {
            if (string.IsNullOrEmpty(bufferName))
            {
                throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
            }

            try
            {
                // Освобождаем ресурсы файла отображения в памяти
                if (_buffers.TryGetValue(bufferName, out var buffer))
                {
                    buffer.Dispose();
                    _buffers.Remove(bufferName);
                }

                // Освобождаем ресурсы события
                if (_events.TryGetValue(bufferName, out var eventHandle))
                {
                    eventHandle.Dispose();
                    _events.Remove(bufferName);
                }

                _logger.LogDebug("Освобожден буфер {BufferName}", bufferName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при освобождении буфера {BufferName}", bufferName);
                throw;
            }
        }

        public void Write(string bufferName, byte[] data, int offset, int count)
        {
            if (string.IsNullOrEmpty(bufferName))
            {
                throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException("Некорректные значения для offset и count");
            }

            try
            {
                if (!_buffers.TryGetValue(bufferName, out var buffer))
                {
                    throw new InvalidOperationException($"Буфер {bufferName} не зарегистрирован");
                }

                using var accessor = buffer.CreateViewAccessor();
                
                // Сначала записываем размер данных
                accessor.Write(0, count);
                
                // Затем записываем сами данные
                accessor.WriteArray(sizeof(int), data, offset, count);
                
                // Сигнализируем о наличии новых данных
                if (_events.TryGetValue(bufferName, out var eventHandle))
                {
                    eventHandle.Set();
                }
                
                _logger.LogTrace("Записано {Count} байт в буфер {BufferName}", count, bufferName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при записи в буфер {BufferName}", bufferName);
                throw;
            }
        }

        public byte[] Read(string bufferName, int offset, int count)
        {
            if (string.IsNullOrEmpty(bufferName))
            {
                throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
            }

            try
            {
                if (!_buffers.TryGetValue(bufferName, out var buffer))
                {
                    throw new InvalidOperationException($"Буфер {bufferName} не зарегистрирован");
                }

                using var accessor = buffer.CreateViewAccessor();
                
                // Читаем размер данных
                int size = accessor.ReadInt32(0);
                
                // Проверяем, что размер корректный
                if (size <= 0 || size > count)
                {
                    throw new InvalidOperationException(
                        $"Некорректный размер данных в буфере: {size}, ожидалось не более {count}");
                }
                
                // Читаем данные
                byte[] data = new byte[size];
                accessor.ReadArray(sizeof(int), data, 0, size);
                
                _logger.LogTrace("Прочитано {Size} байт из буфера {BufferName}", size, bufferName);
                
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении из буфера {BufferName}", bufferName);
                throw;
            }
        }

        public async Task<byte[]> WaitAndReadAsync(string bufferName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(bufferName))
            {
                throw new ArgumentException("Имя буфера не может быть пустым", nameof(bufferName));
            }

            try
            {
                if (!_events.TryGetValue(bufferName, out var eventHandle))
                {
                    throw new InvalidOperationException($"Событие для буфера {bufferName} не зарегистрировано");
                }

                // Ожидаем сигнала о наличии новых данных
                await Task.Run(() => 
                {
                    WaitHandle.WaitAny(new[] { eventHandle, cancellationToken.WaitHandle });
                    cancellationToken.ThrowIfCancellationRequested();
                }, cancellationToken);

                // Читаем данные из буфера
                if (!_buffers.TryGetValue(bufferName, out var buffer))
                {
                    throw new InvalidOperationException($"Буфер {bufferName} не зарегистрирован");
                }

                using var accessor = buffer.CreateViewAccessor();
                
                // Читаем размер данных
                int size = accessor.ReadInt32(0);
                
                // Проверяем, что размер корректный
                if (size <= 0)
                {
                    _logger.LogWarning("Обнаружен некорректный размер данных в буфере {BufferName}: {Size}", 
                        bufferName, size);
                    return Array.Empty<byte>();
                }
                
                // Читаем данные
                byte[] data = new byte[size];
                accessor.ReadArray(sizeof(int), data, 0, size);
                
                _logger.LogTrace("Успешно прочитано {Size} байт из буфера {BufferName} после ожидания", 
                    size, bufferName);
                
                return data;
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Операция ожидания для буфера {BufferName} была отменена", bufferName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при ожидании и чтении из буфера {BufferName}", bufferName);
                throw;
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
                // Освобождаем ресурсы
                foreach (var buffer in _buffers.Values)
                {
                    buffer.Dispose();
                }
                _buffers.Clear();

                foreach (var eventHandle in _events.Values)
                {
                    eventHandle.Dispose();
                }
                _events.Clear();
            }

            _disposed = true;
        }

        ~SharedMemoryManager()
        {
            Dispose(false);
        }
    }
}