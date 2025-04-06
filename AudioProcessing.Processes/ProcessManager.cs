using System.Collections.Concurrent;
using System.Diagnostics;
using AudioProcessing.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AudioProcessing.Processes
{
    public class ProcessManager : IProcessManager
    {
        private readonly ILogger<ProcessManager> _logger;
        private readonly ConcurrentDictionary<int, Process> _processes = new();

        public ProcessManager(ILogger<ProcessManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Нет await операции что делать с этим
        public async Task<int> StartProcessAsync(string executablePath, string arguments)
        {
            if (string.IsNullOrEmpty(executablePath))
                throw new ArgumentException("Путь к исполняемому файлу не может быть пустым", nameof(executablePath));

            try
            {
                // Получаем полный путь к исполняемому файлу
                string fullPath = Path.GetFullPath(executablePath);
                
                // Проверяем существование файла
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Исполняемый файл не найден: {fullPath}");
                }

                // Создаем объект Process
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = fullPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = new Process { StartInfo = processStartInfo };

                // Обработчики событий для логирования
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogDebug("Приемник (PID: {ProcessId}): {Message}", process.Id, e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogError("Приемник (PID: {ProcessId}) ошибка: {Message}", process.Id, e.Data);
                };

                // Запускаем процесс
                if (!process.Start())
                {
                    throw new InvalidOperationException($"Не удалось запустить процесс: {fullPath}");
                }

                // Начинаем асинхронное чтение вывода
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Сохраняем процесс в словаре
                _processes[process.Id] = process;

                _logger.LogInformation("Запущен процесс: {ExecutablePath}, PID: {ProcessId}", 
                    executablePath, process.Id);

                // Возвращаем идентификатор процесса
                return process.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при запуске процесса: {ExecutablePath}", executablePath);
                throw;
            }
        }

        public async Task StopProcessAsync(int processId)
        {
            try
            {
                if (_processes.TryRemove(processId, out var process))
                {
                    if (!process.HasExited)
                    {
                        // Пытаемся корректно завершить процесс
                        process.CloseMainWindow();
                        
                        // Ждем некоторое время для корректного закрытия
                        await Task.Delay(1000);

                        // Если процесс все еще работает, завершаем принудительно
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }

                    // Освобождаем ресурсы
                    process.Dispose();
                    
                    _logger.LogInformation("Процесс остановлен, PID: {ProcessId}", processId);
                }
                else
                {
                    _logger.LogWarning("Процесс с PID {ProcessId} не найден или уже остановлен", processId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при остановке процесса с PID {ProcessId}", processId);
                throw;
            }
        }

        public bool IsProcessRunning(int processId)
        {
            if (_processes.TryGetValue(processId, out var process))
            {
                try
                {
                    return !process.HasExited;
                }
                catch (Exception)
                {
                    // залогируй ошибки
                    return false;
                }
            }
            return false;
        }
    }
}