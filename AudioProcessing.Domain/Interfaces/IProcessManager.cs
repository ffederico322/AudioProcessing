namespace AudioProcessing.Domain.Interfaces;

public interface IProcessManager
{
    Task<int> StartProcessAsync(string executablePath, string arguments);
    Task StopProcessAsync(int processId);
    bool IsProcessRunning(int processId);
}