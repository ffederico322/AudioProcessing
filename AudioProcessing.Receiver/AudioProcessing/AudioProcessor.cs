using AudioProcessing.Receiver.Utils;
using System;

namespace AudioProcessing.Receiver.AudioProcessing
{
    public class AudioProcessor : IDisposable
    {
        private readonly ConsoleLogger _logger;
        private bool _disposed;
        private float _volumeMultiplier = 1.5f; // Коэффициент усиления громкости

        public AudioProcessor(ConsoleLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("Инициализирован процессор аудио");
        }

        public byte[] ProcessAudio(byte[] inputData)
        {
            if (inputData == null || inputData.Length == 0)
            {
                return Array.Empty<byte>();
            }

            _logger.LogDebug($"Обработка аудио-данных, размер: {inputData.Length} байт");

            try
            {
                // Предполагаем, что аудио в формате PCM 16-bit
                // Преобразуем байты в 16-битные сэмплы
                byte[] outputData = new byte[inputData.Length];
                
                for (int i = 0; i < inputData.Length; i += 2)
                {
                    if (i + 1 < inputData.Length)
                    {
                        // Преобразуем два байта в одно 16-битное значение (little-endian)
                        short sample = (short)((inputData[i + 1] << 8) | inputData[i]);
                        
                        // Применяем изменение громкости
                        float newSample = sample * _volumeMultiplier;
                        
                        // Ограничиваем значение, чтобы избежать переполнения
                        if (newSample > short.MaxValue) newSample = short.MaxValue;
                        if (newSample < short.MinValue) newSample = short.MinValue;
                        
                        // Преобразуем обратно в байты
                        short processedSample = (short)newSample;
                        outputData[i] = (byte)(processedSample & 0xFF);
                        outputData[i + 1] = (byte)((processedSample >> 8) & 0xFF);
                    }
                }

                _logger.LogDebug($"Аудио-данные успешно обработаны");
                return outputData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при обработке аудио: {ex.Message}");
                // В случае ошибки возвращаем исходные данные
                return inputData;
            }
        }

        public void SetVolumeMultiplier(float multiplier)
        {
            if (multiplier < 0)
            {
                throw new ArgumentException("Множитель громкости не может быть отрицательным", nameof(multiplier));
            }
            
            _volumeMultiplier = multiplier;
            _logger.LogInformation($"Установлен новый множитель громкости: {multiplier}");
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
                // Освобождаем ресурсы, если необходимо
                // Например, закрываем соединения с аудио-устройствами
            }

            _disposed = true;
        }

        ~AudioProcessor()
        {
            Dispose(false);
        }
    }
}